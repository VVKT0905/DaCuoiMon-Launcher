using System;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using CobblemonLauncher.Models;

namespace CobblemonLauncher.Services
{
    public static class RemoteConfigService
    {
        private static readonly HttpClient HttpClient = new HttpClient();
        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            WriteIndented = true
        };

        static RemoteConfigService()
        {
            HttpClient.DefaultRequestHeaders.UserAgent.ParseAdd("CobblemonLauncherDotNet/1.0");
        }

        public static async Task<RemoteConfig> FetchConfigAsync(string url, string cacheDir)
        {
            string cachePath = Path.Combine(cacheDir, "remote_config_cache.json");

            try
            {
                using var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(8));
                using var response = await HttpClient.GetAsync(url, cts.Token);
                if (response.IsSuccessStatusCode)
                {
                    string raw = await response.Content.ReadAsStringAsync();
                    raw = raw.Trim().Trim('\uFEFF', '\u200B', '\uFEFE');

                    RemoteConfig? config = null;

                    // Try parsing as JSON (resilient to BOM and whitespace)
                    int firstBrace = raw.IndexOf('{');
                    int lastBrace = raw.LastIndexOf('}');
                    if (firstBrace >= 0 && lastBrace > firstBrace)
                    {
                        try
                        {
                            string jsonSpan = raw.Substring(firstBrace, lastBrace - firstBrace + 1);
                            config = JsonSerializer.Deserialize<RemoteConfig>(jsonSpan, JsonOptions);
                        }
                        catch { }
                    }

                    // Fallback: If not JSON but raw IP string
                    if (config == null && !string.IsNullOrWhiteSpace(raw))
                    {
                        RemoteConfig? baseConfig = null;
                        if (File.Exists(cachePath))
                        {
                            try { baseConfig = JsonSerializer.Deserialize<RemoteConfig>(File.ReadAllText(cachePath), JsonOptions); } catch { }
                        }
                        if (baseConfig == null || baseConfig.Skins == null || baseConfig.Skins.Count == 0)
                        {
                            string localTestIp = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "testip.json");
                            if (File.Exists(localTestIp))
                            {
                                try { baseConfig = JsonSerializer.Deserialize<RemoteConfig>(File.ReadAllText(localTestIp), JsonOptions); } catch { }
                            }
                        }

                        config = baseConfig ?? new RemoteConfig();
                        config.ServerIp = raw;
                    }

                    if (config != null)
                    {
                        // Cache successful config locally
                        try
                        {
                            Directory.CreateDirectory(cacheDir);
                            await File.WriteAllTextAsync(cachePath, JsonSerializer.Serialize(config, JsonOptions));
                        }
                        catch { }

                        return config;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[RemoteConfigService] Lỗi tải config từ xa: {ex.Message}");
            }

            // Fallback to local cache if offline
            if (File.Exists(cachePath))
            {
                try
                {
                    string cachedJson = await File.ReadAllTextAsync(cachePath);
                    var cachedConfig = JsonSerializer.Deserialize<RemoteConfig>(cachedJson, JsonOptions);
                    if (cachedConfig != null) return cachedConfig;
                }
                catch { }
            }

            // Fallback to default empty config
            return new RemoteConfig();
        }
    }
}
