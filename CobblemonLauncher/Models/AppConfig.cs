using System;
using System.IO;
using System.Text.Json;

namespace CobblemonLauncher.Models
{
    public class AppConfig
    {
        public string PlayerName { get; set; } = "PixelMaster7";
        public string AuthMode { get; set; } = "Offline";
        public string MicrosoftAccessToken { get; set; } = "";
        public string MicrosoftUuid { get; set; } = "";
        public string MicrosoftUsername { get; set; } = "";
        public string ServerAddress { get; set; } = "127.0.0.1:25565";
        public int AllocatedRamGb { get; set; } = 4;
        public bool AutoDirectConnect { get; set; } = true;
        public string CustomJavaPath { get; set; } = "";
        public string GameDirectory { get; set; } = "";
        public int CobbleCoins { get; set; } = 0;
        public string LastDailyLoginDate { get; set; } = "";
        public int CheckInStreak { get; set; } = 0;
        public int TotalPlayTimeSeconds { get; set; } = 0;
        public System.Collections.Generic.List<string> OwnedSkinIds { get; set; } = new() { "default" };
        public string SelectedSkinId { get; set; } = "default";
        public System.Collections.Generic.List<string> RedeemedGiftcodes { get; set; } = new();
        public System.Collections.Generic.Dictionary<string, DateTime> TrialSkins { get; set; } = new();

        public static bool IsAscii(string value)
        {
            foreach (char c in value)
            {
                if (c > 127) return false;
            }
            return true;
        }

        public string GetEffectiveGameDir()
        {
            if (!string.IsNullOrWhiteSpace(GameDirectory) && Directory.Exists(GameDirectory))
                return GameDirectory;

            // Prioritize storing all game data inside the app's installation folder (data/instance)
            string localDataDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data");
            if (IsAscii(localDataDir))
            {
                string localInstance = Path.Combine(localDataDir, "instance");
                if (!Directory.Exists(localInstance)) Directory.CreateDirectory(localInstance);
                return localInstance;
            }

            // Fallback for dev environment if working folder has non-ASCII characters
            string defaultPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "CobblemonLauncher",
                "instance"
            );
            if (!Directory.Exists(defaultPath))
            {
                Directory.CreateDirectory(defaultPath);
            }
            return defaultPath;
        }

        private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

        public static string GetConfigFilePath()
        {
            return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.json");
        }

        public static AppConfig Load()
        {
            string path = GetConfigFilePath();
            if (File.Exists(path))
            {
                try
                {
                    string json = File.ReadAllText(path);
                    var cfg = JsonSerializer.Deserialize<AppConfig>(json, JsonOptions);
                    if (cfg != null) return cfg;
                }
                catch { }
            }

            // Check legacy Program Files config for smooth migration
            try
            {
                string legacyPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "DaCuoiMon", "config.json");
                if (File.Exists(legacyPath))
                {
                    string json = File.ReadAllText(legacyPath);
                    var cfg = JsonSerializer.Deserialize<AppConfig>(json, JsonOptions);
                    if (cfg != null)
                    {
                        cfg.Save();
                        return cfg;
                    }
                }
            }
            catch { }

            var defaultConfig = new AppConfig();
            defaultConfig.Save();
            return defaultConfig;
        }

        public void Save()
        {
            try
            {
                string path = GetConfigFilePath();
                string json = JsonSerializer.Serialize(this, JsonOptions);
                File.WriteAllText(path, json);
            }
            catch { }
        }
    }
}
