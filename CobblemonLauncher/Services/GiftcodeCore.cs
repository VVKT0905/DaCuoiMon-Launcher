using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CobblemonLauncher.Services
{
    public class GiftcodeTrialSkin
    {
        [JsonPropertyName("id")]
        public string SkinId { get; set; } = "";

        [JsonPropertyName("hours")]
        public double Hours { get; set; } = 24;
    }

    public class GiftcodePayload
    {
        [JsonPropertyName("id")]
        public string CodeId { get; set; } = Guid.NewGuid().ToString("N");

        [JsonPropertyName("coins")]
        public int Coins { get; set; } = 0;

        [JsonPropertyName("skins")]
        public List<string> PermanentSkins { get; set; } = new();

        [JsonPropertyName("trials")]
        public List<GiftcodeTrialSkin> TrialSkins { get; set; } = new();

        [JsonPropertyName("exp")]
        public long? ExpiresAtUnix { get; set; } // Unix timestamp seconds, null = no expiry

        [JsonPropertyName("salt")]
        public string Salt { get; set; } = Guid.NewGuid().ToString("N")[..8];
    }

    public static class GiftcodeCore
    {
        private static readonly byte[] SecretKey = SHA256.HashData(
            Encoding.UTF8.GetBytes("DaCuoiMon@Giftcode_Secret_Key#2026!SecureToken")
        );

        public static string EncryptGiftcode(GiftcodePayload payload)
        {
            if (string.IsNullOrWhiteSpace(payload.CodeId)) payload.CodeId = Guid.NewGuid().ToString("N");
            if (string.IsNullOrWhiteSpace(payload.Salt)) payload.Salt = Guid.NewGuid().ToString("N")[..8];

            string json = JsonSerializer.Serialize(payload);
            byte[] jsonBytes = Encoding.UTF8.GetBytes(json);

            // 1. Compress JSON with GZip
            byte[] compressed;
            using (var ms = new MemoryStream())
            {
                using (var gz = new GZipStream(ms, CompressionLevel.Optimal, true))
                {
                    gz.Write(jsonBytes, 0, jsonBytes.Length);
                }
                compressed = ms.ToArray();
            }

            // 2. Encrypt AES-256-CBC
            using var aes = Aes.Create();
            aes.Key = SecretKey;
            aes.GenerateIV();
            byte[] iv = aes.IV;

            byte[] encrypted;
            using (var ms = new MemoryStream())
            {
                ms.Write(iv, 0, iv.Length);
                using (var cs = new CryptoStream(ms, aes.CreateEncryptor(), CryptoStreamMode.Write))
                {
                    cs.Write(compressed, 0, compressed.Length);
                    cs.FlushFinalBlock();
                }
                encrypted = ms.ToArray();
            }

            // 3. Base64Url encode with DCM- prefix
            string base64 = Convert.ToBase64String(encrypted)
                .Replace('+', '-')
                .Replace('/', '_')
                .TrimEnd('=');

            return $"DCM-{base64}";
        }

        public static GiftcodePayload DecryptGiftcode(string code)
        {
            if (string.IsNullOrWhiteSpace(code))
                throw new ArgumentException("Vui lòng nhập mã Giftcode!");

            string raw = code.Trim();
            if (raw.StartsWith("DCM-", StringComparison.OrdinalIgnoreCase))
            {
                raw = raw[4..];
            }

            raw = raw.Replace('-', '+').Replace('_', '/');
            switch (raw.Length % 4)
            {
                case 2: raw += "=="; break;
                case 3: raw += "="; break;
            }

            byte[] encrypted;
            try
            {
                encrypted = Convert.FromBase64String(raw);
            }
            catch
            {
                throw new FormatException("Mã Giftcode không đúng định dạng!");
            }

            if (encrypted.Length < 17)
                throw new FormatException("Mã Giftcode không hợp lệ hoặc bị hỏng!");

            byte[] iv = new byte[16];
            Array.Copy(encrypted, 0, iv, 0, 16);
            int cipherLength = encrypted.Length - 16;

            byte[] decompressed;
            try
            {
                using var aes = Aes.Create();
                aes.Key = SecretKey;
                aes.IV = iv;

                using var msCipher = new MemoryStream(encrypted, 16, cipherLength);
                using var cs = new CryptoStream(msCipher, aes.CreateDecryptor(), CryptoStreamMode.Read);
                using var gz = new GZipStream(cs, CompressionMode.Decompress);
                using var msOut = new MemoryStream();
                gz.CopyTo(msOut);
                decompressed = msOut.ToArray();
            }
            catch
            {
                throw new CryptographicException("Giải mã thất bại! Mã Giftcode không hợp lệ hoặc sai khóa bảo mật.");
            }

            string json = Encoding.UTF8.GetString(decompressed);
            var payload = JsonSerializer.Deserialize<GiftcodePayload>(json);
            if (payload == null || string.IsNullOrWhiteSpace(payload.CodeId))
                throw new InvalidDataException("Dữ liệu Giftcode không hợp lệ!");

            return payload;
        }
    }
}
