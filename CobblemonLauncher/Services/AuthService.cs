using System;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace CobblemonLauncher.Services
{
    public class PlayerSession
    {
        public string Username { get; set; } = "Player";
        public string Uuid { get; set; } = "";
        public string AccessToken { get; set; } = "0";
        public string UserType { get; set; } = "mojang";
    }

    public class DeviceCodeResult
    {
        public string UserCode { get; set; } = "";
        public string DeviceCode { get; set; } = "";
        public string VerificationUri { get; set; } = "";
        public int ExpiresIn { get; set; } = 900;
        public int Interval { get; set; } = 5;
    }

    public class AuthService
    {
        private static readonly HttpClient HttpClient = new HttpClient();
        private const string ClientId = "00000000402b5328"; // Minecraft public client ID for desktop

        public static PlayerSession CreateOfflineSession(string username)
        {
            if (string.IsNullOrWhiteSpace(username))
                username = "Player";

            username = username.Trim();

            // Generate deterministic UUIDv3 (MD5 of OfflinePlayer:username)
            byte[] hash;
            using (var md5 = MD5.Create())
            {
                hash = md5.ComputeHash(Encoding.UTF8.GetBytes("OfflinePlayer:" + username));
            }

            // Set version to 3 and variant to IETF
            hash[6] = (byte)((hash[6] & 0x0f) | 0x30);
            hash[8] = (byte)((hash[8] & 0x3f) | 0x80);

            var sb = new StringBuilder();
            for (int i = 0; i < 16; i++)
            {
                if (i == 4 || i == 6 || i == 8 || i == 10)
                    sb.Append("-");
                sb.Append(hash[i].ToString("x2"));
            }

            return new PlayerSession
            {
                Username = username,
                Uuid = sb.ToString(),
                AccessToken = "0",
                UserType = "legacy"
            };
        }

        public static async Task<DeviceCodeResult> RequestDeviceCodeAsync()
        {
            var content = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("client_id", ClientId),
                new KeyValuePair<string, string>("scope", "service::user.auth.xboxlive.com::MBI_SSL")
            });

            var resp = await HttpClient.PostAsync("https://login.microsoftonline.com/consumers/oauth2/v2.0/devicecode", content);
            resp.EnsureSuccessStatusCode();

            var json = await resp.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            return new DeviceCodeResult
            {
                UserCode = root.GetProperty("user_code").GetString() ?? "",
                DeviceCode = root.GetProperty("device_code").GetString() ?? "",
                VerificationUri = root.GetProperty("verification_uri").GetString() ?? "https://microsoft.com/link",
                ExpiresIn = root.GetProperty("expires_in").GetInt32(),
                Interval = root.TryGetProperty("interval", out var inv) ? inv.GetInt32() : 5
            };
        }

        public static async Task<PlayerSession> PollMicrosoftAuthAsync(DeviceCodeResult deviceCode, CancellationToken ct)
        {
            int interval = Math.Max(deviceCode.Interval, 5);
            DateTime expiry = DateTime.UtcNow.AddSeconds(deviceCode.ExpiresIn);

            string? msAccessToken = null;

            while (DateTime.UtcNow < expiry && !ct.IsCancellationRequested)
            {
                await Task.Delay(interval * 1000, ct);

                var tokenReq = new FormUrlEncodedContent(new[]
                {
                    new KeyValuePair<string, string>("grant_type", "urn:ietf:params:oauth:grant-type:device_code"),
                    new KeyValuePair<string, string>("client_id", ClientId),
                    new KeyValuePair<string, string>("device_code", deviceCode.DeviceCode)
                });

                var resp = await HttpClient.PostAsync("https://login.microsoftonline.com/consumers/oauth2/v2.0/token", tokenReq, ct);
                var json = await resp.Content.ReadAsStringAsync(ct);

                if (resp.IsSuccessStatusCode)
                {
                    using var doc = JsonDocument.Parse(json);
                    msAccessToken = doc.RootElement.GetProperty("access_token").GetString();
                    break;
                }
                else
                {
                    using var doc = JsonDocument.Parse(json);
                    string err = doc.RootElement.TryGetProperty("error", out var e) ? e.GetString() ?? "" : "";
                    if (err != "authorization_pending")
                    {
                        throw new Exception($"Lỗi xác thực Microsoft: {err}");
                    }
                }
            }

            if (string.IsNullOrEmpty(msAccessToken))
                throw new TimeoutException("Hết hạn xác thực tài khoản Microsoft!");

            // 1. Authenticate with Xbox Live
            var xblReq = new
            {
                Properties = new
                {
                    AuthMethod = "RPS",
                    SiteName = "user.auth.xboxlive.com",
                    RpsTicket = $"d={msAccessToken}"
                },
                RelyingParty = "http://auth.xboxlive.com",
                TokenType = "JWT"
            };

            var xblResp = await HttpClient.PostAsync(
                "https://user.auth.xboxlive.com/user/authenticate",
                new StringContent(JsonSerializer.Serialize(xblReq), Encoding.UTF8, "application/json"),
                ct
            );
            xblResp.EnsureSuccessStatusCode();
            var xblJson = await xblResp.Content.ReadAsStringAsync(ct);
            using var xblDoc = JsonDocument.Parse(xblJson);
            string xblToken = xblDoc.RootElement.GetProperty("Token").GetString()!;
            string userHash = xblDoc.RootElement.GetProperty("DisplayClaims").GetProperty("xui")[0].GetProperty("uhs").GetString()!;

            // 2. Obtain XSTS Token
            var xstsReq = new
            {
                Properties = new
                {
                    SandboxId = "RETAIL",
                    UserTokens = new[] { xblToken }
                },
                RelyingParty = "rp://api.minecraftservices.com/",
                TokenType = "JWT"
            };

            var xstsResp = await HttpClient.PostAsync(
                "https://xsts.auth.xboxlive.com/xsts/authorize",
                new StringContent(JsonSerializer.Serialize(xstsReq), Encoding.UTF8, "application/json"),
                ct
            );
            xstsResp.EnsureSuccessStatusCode();
            var xstsJson = await xstsResp.Content.ReadAsStringAsync(ct);
            using var xstsDoc = JsonDocument.Parse(xstsJson);
            string xstsToken = xstsDoc.RootElement.GetProperty("Token").GetString()!;

            // 3. Login with Minecraft Services
            var mcLoginReq = new
            {
                identityToken = $"XBL3.0 x={userHash};{xstsToken}"
            };

            var mcLoginResp = await HttpClient.PostAsync(
                "https://api.minecraftservices.com/authentication/login_with_xbox",
                new StringContent(JsonSerializer.Serialize(mcLoginReq), Encoding.UTF8, "application/json"),
                ct
            );
            mcLoginResp.EnsureSuccessStatusCode();
            var mcLoginJson = await mcLoginResp.Content.ReadAsStringAsync(ct);
            using var mcDoc = JsonDocument.Parse(mcLoginJson);
            string mcToken = mcDoc.RootElement.GetProperty("access_token").GetString()!;

            // 4. Get Profile
            using var profileReq = new HttpRequestMessage(HttpMethod.Get, "https://api.minecraftservices.com/minecraft/profile");
            profileReq.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", mcToken);

            var profileResp = await HttpClient.SendAsync(profileReq, ct);
            profileResp.EnsureSuccessStatusCode();
            var profileJson = await profileResp.Content.ReadAsStringAsync(ct);
            using var profileDoc = JsonDocument.Parse(profileJson);
            string profileName = profileDoc.RootElement.GetProperty("name").GetString()!;
            string profileId = profileDoc.RootElement.GetProperty("id").GetString()!;

            return new PlayerSession
            {
                Username = profileName,
                Uuid = profileId,
                AccessToken = mcToken,
                UserType = "mojang"
            };
        }
    }
}
