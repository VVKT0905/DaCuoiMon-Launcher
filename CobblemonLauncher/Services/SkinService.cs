using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using CobblemonLauncher.Models;

namespace CobblemonLauncher.Services
{
    public static class SkinService
    {
        private static readonly HttpClient HttpClient = new HttpClient();

        static SkinService()
        {
            HttpClient.DefaultRequestHeaders.UserAgent.ParseAdd("CobblemonLauncherDotNet/1.0");
        }

        public static readonly List<SkinItem> DefaultCatalogue = new()
        {
            new SkinItem
            {
                Id = "red_trainer",
                Name = "Red (Kanto Champion)",
                Description = "Trang phục Huấn luyện viên huyền thoại vùng Kanto",
                Price = 1000,
                FileName = "red_trainer.ysm",
                DownloadUrl = "https://raw.githubusercontent.com/VVKT0905/AIS-POS-Smart-Supermarket/refs/heads/master/models/red.ysm"
            },
            new SkinItem
            {
                Id = "ash_ketchum",
                Name = "Ash Ketchum (Pallet)",
                Description = "Bộ trang phục Satoshi cổ điển với mũ Pokéball",
                Price = 1000,
                FileName = "ash_ketchum.ysm",
                DownloadUrl = "https://raw.githubusercontent.com/VVKT0905/AIS-POS-Smart-Supermarket/refs/heads/master/models/ash.ysm"
            },
            new SkinItem
            {
                Id = "serena_kalos",
                Name = "Serena (Kalos Performer)",
                Description = "Trang phục nữ chính thanh lịch vùng Kalos",
                Price = 1000,
                FileName = "serena.ysm",
                DownloadUrl = "https://raw.githubusercontent.com/VVKT0905/AIS-POS-Smart-Supermarket/refs/heads/master/models/serena.ysm"
            },
            new SkinItem
            {
                Id = "pikachu_onesie",
                Name = "Pikachu Onesie Hoodie",
                Description = "Bộ đồ ngủ Pikachu liền thân siêu dễ thương",
                Price = 1000,
                FileName = "pikachu_hoodie.ysm",
                DownloadUrl = "https://raw.githubusercontent.com/VVKT0905/AIS-POS-Smart-Supermarket/refs/heads/master/models/pikachu.ysm"
            },
            new SkinItem
            {
                Id = "lucario_aura",
                Name = "Lucario Aura Trainer",
                Description = "Trang phục chiến binh làn sóng hào quang Lucario",
                Price = 1000,
                FileName = "lucario_aura.ysm",
                DownloadUrl = "https://raw.githubusercontent.com/VVKT0905/AIS-POS-Smart-Supermarket/refs/heads/master/models/lucario.ysm"
            }
        };

        public static (bool canClaim, int nextStreakDay, int reward) GetDailyCheckInStatus(AppConfig config)
        {
            string today = DateTime.Now.ToString("yyyy-MM-dd");
            string yesterday = DateTime.Now.AddDays(-1).ToString("yyyy-MM-dd");

            bool alreadyClaimedToday = string.Equals(config.LastDailyLoginDate, today, StringComparison.OrdinalIgnoreCase);
            if (alreadyClaimedToday)
            {
                int currentDay = config.CheckInStreak <= 0 ? 1 : config.CheckInStreak;
                return (false, currentDay, currentDay == 7 ? 3000 : 1000);
            }

            int nextDay = 1;
            if (string.Equals(config.LastDailyLoginDate, yesterday, StringComparison.OrdinalIgnoreCase))
            {
                nextDay = (config.CheckInStreak % 7) + 1;
            }

            int nextReward = nextDay == 7 ? 3000 : 1000;
            return (true, nextDay, nextReward);
        }

        public static (bool awarded, int amount, int streakDay) ClaimDailyCheckIn(AppConfig config)
        {
            var (canClaim, nextDay, reward) = GetDailyCheckInStatus(config);
            if (!canClaim) return (false, 0, config.CheckInStreak);

            config.CheckInStreak = nextDay;
            config.CobbleCoins += reward;
            config.LastDailyLoginDate = DateTime.Now.ToString("yyyy-MM-dd");
            config.Save();

            return (true, reward, config.CheckInStreak);
        }

        public static (bool awarded, int amount) CheckAndAwardDailyLogin(AppConfig config)
        {
            var (awarded, amount, _) = ClaimDailyCheckIn(config);
            return (awarded, amount);
        }

        public static void AwardPlayTime(AppConfig config, int seconds = 300, int coins = 100)
        {
            config.CobbleCoins += coins;
            config.TotalPlayTimeSeconds += seconds;
            config.Save();
        }

        public static void CheckAndExpireTrialSkins(AppConfig config, string gameDir)
        {
            if (config.TrialSkins == null || config.TrialSkins.Count == 0) return;

            var now = DateTime.UtcNow;
            var expired = new List<string>();

            foreach (var (skinId, expiry) in config.TrialSkins)
            {
                if (now >= expiry)
                {
                    expired.Add(skinId);
                }
            }

            if (expired.Count > 0)
            {
                var allSkins = GetEffectiveSkins(null, gameDir);
                foreach (var skinId in expired)
                {
                    config.TrialSkins.Remove(skinId);
                    config.OwnedSkinIds.Remove(skinId);

                    var skinObj = allSkins.FirstOrDefault(s => string.Equals(s.Id, skinId, StringComparison.OrdinalIgnoreCase));
                    if (skinObj != null && !string.IsNullOrWhiteSpace(skinObj.FileName))
                    {
                        string customFile = Path.Combine(gameDir, "config", "yes_steve_model", "custom", skinObj.FileName);
                        if (File.Exists(customFile))
                        {
                            try { File.Delete(customFile); } catch { }
                        }
                    }
                }
                config.Save();
            }
        }

        public static List<SkinItem> GetEffectiveSkins(List<SkinItem>? remoteSkins, string? gameDir = null)
        {
            var skinDict = new Dictionary<string, SkinItem>(StringComparer.OrdinalIgnoreCase);

            // 1. Add skins from remote config if provided
            if (remoteSkins != null)
            {
                foreach (var s in remoteSkins)
                {
                    if (!string.IsNullOrWhiteSpace(s.FileName))
                    {
                        skinDict[s.FileName] = s;
                    }
                }
            }

            // 2. Check local testip.json or cache if skinDict is empty
            if (skinDict.Count == 0)
            {
                var fallbackJsonPaths = new List<string>
                {
                    Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "testip.json"),
                    Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "remote_config_cache.json")
                };
                if (!string.IsNullOrWhiteSpace(gameDir))
                {
                    fallbackJsonPaths.Add(Path.Combine(gameDir, "testip.json"));
                    fallbackJsonPaths.Add(Path.Combine(gameDir, "remote_config_cache.json"));
                }

                foreach (var jsonPath in fallbackJsonPaths)
                {
                    if (File.Exists(jsonPath))
                    {
                        try
                        {
                            string json = File.ReadAllText(jsonPath);
                            var localCfg = System.Text.Json.JsonSerializer.Deserialize<RemoteConfig>(json, new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                            if (localCfg?.Skins != null && localCfg.Skins.Count > 0)
                            {
                                foreach (var s in localCfg.Skins)
                                {
                                    if (!string.IsNullOrWhiteSpace(s.FileName))
                                        skinDict[s.FileName] = s;
                                }
                                break;
                            }
                        }
                        catch { }
                    }
                }
            }

            // 3. Scan models_pool directories for any present .ysm models
            var searchDirs = new List<string>();
            if (!string.IsNullOrWhiteSpace(gameDir))
            {
                searchDirs.Add(Path.Combine(gameDir, "models_pool"));
            }
            searchDirs.Add(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "models_pool"));

            // Project root folder if running in dev environment
            try
            {
                string devPool = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "YSM_Models_Pool"));
                if (Directory.Exists(devPool)) searchDirs.Add(devPool);
            }
            catch { }

            foreach (var dir in searchDirs)
            {
                if (Directory.Exists(dir))
                {
                    foreach (var file in Directory.GetFiles(dir, "*.ysm"))
                    {
                        string fileName = Path.GetFileName(file);
                        if (!skinDict.ContainsKey(fileName))
                        {
                            string baseName = Path.GetFileNameWithoutExtension(file);
                            string safeId = baseName.ToLower().Replace(" ", "_").Replace("-", "_");
                            string friendlyName = baseName.Replace("_", " ").Trim();

                            skinDict[fileName] = new SkinItem
                            {
                                Id = safeId,
                                Name = friendlyName,
                                Description = "Custom Yes Steve Model dành riêng cho Đá Cuội Mon",
                                Price = 1000,
                                FileName = fileName,
                                DownloadUrl = ""
                            };
                        }
                    }
                }
            }

            if (skinDict.Count > 0)
            {
                return skinDict.Values.ToList();
            }

            return DefaultCatalogue;
        }

        public static string? FindYsmFilePath(SkinItem skin, string? gameDir = null)
        {
            if (string.IsNullOrWhiteSpace(skin.FileName)) return null;

            var candidates = new List<string>();
            if (!string.IsNullOrWhiteSpace(gameDir))
            {
                candidates.Add(Path.Combine(gameDir, "config", "yes_steve_model", "custom", skin.FileName));
                candidates.Add(Path.Combine(gameDir, "models_pool", skin.FileName));
            }
            candidates.Add(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "models_pool", skin.FileName));

            try
            {
                string devPool = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "YSM_Models_Pool", skin.FileName));
                candidates.Add(devPool);
                string devPool2 = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "YSM_Models_Pool", skin.FileName));
                candidates.Add(devPool2);
                string devPool3 = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "YSM_Models_Pool", skin.FileName));
                candidates.Add(devPool3);
            }
            catch { }

            return candidates.FirstOrDefault(File.Exists);
        }

        public static async Task<bool> RedeemSkinAsync(AppConfig config, SkinItem skin, string gameDir)
        {
            if (config.CobbleCoins < skin.Price)
                return false;

            config.CobbleCoins -= skin.Price;
            if (!config.OwnedSkinIds.Contains(skin.Id))
            {
                config.OwnedSkinIds.Add(skin.Id);
            }
            config.SelectedSkinId = skin.Id;
            config.Save();

            // Install YSM model file into custom models directory
            await InstallYsmModelAsync(skin, gameDir);
            return true;
        }

        public static async Task InstallYsmModelAsync(SkinItem skin, string gameDir)
        {
            if (string.IsNullOrWhiteSpace(skin.FileName)) return;

            string ysmCustomDir = Path.Combine(gameDir, "config", "yes_steve_model", "custom");
            Directory.CreateDirectory(ysmCustomDir);

            string destFile = Path.Combine(ysmCustomDir, skin.FileName);
            if (File.Exists(destFile)) return;

            // 1. Try copying from local models_pool in gameDir
            string poolFileInGame = Path.Combine(gameDir, "models_pool", skin.FileName);
            if (File.Exists(poolFileInGame))
            {
                try
                {
                    File.Copy(poolFileInGame, destFile, true);
                    return;
                }
                catch { }
            }

            // 2. Try copying from models_pool next to launcher
            string poolFileLauncher = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "models_pool", skin.FileName);
            if (File.Exists(poolFileLauncher))
            {
                try
                {
                    File.Copy(poolFileLauncher, destFile, true);
                    return;
                }
                catch { }
            }

            // 3. Fallback to download if remote URL is available
            if (!string.IsNullOrWhiteSpace(skin.DownloadUrl))
            {
                try
                {
                    using var resp = await HttpClient.GetAsync(skin.DownloadUrl);
                    if (resp.IsSuccessStatusCode)
                    {
                        using var fs = new FileStream(destFile, FileMode.Create, FileAccess.Write, FileShare.None);
                        await resp.Content.CopyToAsync(fs);
                    }
                }
                catch { }
            }
        }

        public static void SelectSkin(AppConfig config, SkinItem skin)
        {
            config.SelectedSkinId = skin.Id;
            config.Save();
        }

        public static void EnforceOwnedModels(AppConfig config, string gameDir, List<SkinItem>? remoteSkins)
        {
            string ysmCustomDir = Path.Combine(gameDir, "config", "yes_steve_model", "custom");
            if (!Directory.Exists(ysmCustomDir))
            {
                Directory.CreateDirectory(ysmCustomDir);
            }

            var effectiveSkins = GetEffectiveSkins(remoteSkins, gameDir);
            var ownedFileNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var skin in effectiveSkins)
            {
                if (config.OwnedSkinIds.Contains(skin.Id))
                {
                    ownedFileNames.Add(skin.FileName);

                    // Ensure the model is present in custom directory if available in local pool
                    string destFile = Path.Combine(ysmCustomDir, skin.FileName);
                    if (!File.Exists(destFile))
                    {
                        string poolGame = Path.Combine(gameDir, "models_pool", skin.FileName);
                        string poolLauncher = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "models_pool", skin.FileName);
                        if (File.Exists(poolGame))
                        {
                            try { File.Copy(poolGame, destFile, true); } catch { }
                        }
                        else if (File.Exists(poolLauncher))
                        {
                            try { File.Copy(poolLauncher, destFile, true); } catch { }
                        }
                    }
                }
            }

            // Strictly remove any unowned / manually dropped model files
            foreach (var file in Directory.GetFiles(ysmCustomDir, "*.*"))
            {
                string fileName = Path.GetFileName(file);
                if (!ownedFileNames.Contains(fileName))
                {
                    try
                    {
                        File.Delete(file);
                    }
                    catch { }
                }
            }

            // Enforce builtin model removal & blacklist so all built-in models require purchasing
            RemoveBuiltinModels(gameDir);
        }

        public static void RemoveBuiltinModels(string? gameDir)
        {
            var candidateDirs = new List<string>();
            if (!string.IsNullOrWhiteSpace(gameDir)) candidateDirs.Add(gameDir);

            candidateDirs.Add(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data", "instance"));
            candidateDirs.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CobblemonLauncher", "instance"));
            candidateDirs.Add(@"C:\Program Files\DaCuoiMon\data\instance");

            foreach (var dir in candidateDirs)
            {
                if (string.IsNullOrWhiteSpace(dir) || !Directory.Exists(dir)) continue;

                try
                {
                    string ysmConfigDir = Path.Combine(dir, "config", "yes_steve_model");
                    if (Directory.Exists(ysmConfigDir))
                    {
                        // 1. Enforce blacklist to prevent YSM from ever extracting builtin models
                        string blacklistPath = Path.Combine(ysmConfigDir, "blacklist.txt");
                        File.WriteAllText(blacklistPath, ".*\n");

                        // 2. Remove builtin folder completely
                        string builtinDir = Path.Combine(ysmConfigDir, "builtin");
                        if (Directory.Exists(builtinDir))
                        {
                            try
                            {
                                Directory.Delete(builtinDir, true);
                            }
                            catch
                            {
                                foreach (var sub in Directory.GetDirectories(builtinDir))
                                {
                                    try { Directory.Delete(sub, true); } catch { }
                                }
                                foreach (var file in Directory.GetFiles(builtinDir))
                                {
                                    try { File.Delete(file); } catch { }
                                }
                            }
                        }
                    }
                }
                catch { }
            }
        }

        public static string GetYsmCustomFolder(string gameDir)
        {
            string ysmCustomDir = Path.Combine(gameDir, "config", "yes_steve_model", "custom");
            Directory.CreateDirectory(ysmCustomDir);
            return ysmCustomDir;
        }
    }
}
