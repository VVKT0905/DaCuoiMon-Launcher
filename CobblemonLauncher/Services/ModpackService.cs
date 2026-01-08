using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using CobblemonLauncher.Models;

namespace CobblemonLauncher.Services
{
    public class ModpackService
    {
        private static readonly HttpClient HttpClient = new HttpClient();

        static ModpackService()
        {
            HttpClient.DefaultRequestHeaders.UserAgent.ParseAdd("CobblemonLauncherDotNet/1.0 (contact@launcher.local)");
        }

        public const string BaseModpackUrl = "https://cdn.modrinth.com/data/5FFgwNNP/versions/Cqimd3JM/Cobblemon%20Modpack%20%5BFabric%5D%201.8.1.mrpack";

        public class ModEntry
        {
            public string Name { get; set; } = "";
            public string Slug { get; set; } = "";
            public string FileName { get; set; } = "";
            public string DownloadUrl { get; set; } = "";
            public long ExpectedSize { get; set; }
            public bool IsDatapack { get; set; }
            public bool IsResourcepack { get; set; }
        }

        public static ModEntry ToModEntry(RemoteModEntry remote)
        {
            return new ModEntry
            {
                Name = remote.Name,
                Slug = remote.Slug,
                FileName = remote.FileName,
                DownloadUrl = remote.DownloadUrl,
                ExpectedSize = remote.ExpectedSize,
                IsDatapack = remote.IsEffectiveDatapack,
                IsResourcepack = remote.IsEffectiveResourcepack
            };
        }

        public static readonly ModEntry[] DefaultAdditionalMods = new[]
        {
            new ModEntry
            {
                Name = "Kazeran Eeveelutions",
                Slug = "kazeran-eeveelutions",
                FileName = "kazeran-eeveelutions-1.5.1.jar",
                DownloadUrl = "https://cdn.modrinth.com/data/l5mIjxVT/versions/lpZfpuhD/kazeran-eeveelutions-1.5.1.jar",
                ExpectedSize = 3264975,
                IsDatapack = false
            },
            new ModEntry
            {
                Name = "Cobblemon Paleontology",
                Slug = "cobblemon-paleontology",
                FileName = "cobblemon-paleontologist-0.7.1-Beta.jar",
                DownloadUrl = "https://cdn.modrinth.com/data/tbBs6vYE/versions/EwdolkEl/cobblemon-paleontologist-0.7.1-Beta.jar",
                ExpectedSize = 223631,
                IsDatapack = false
            },
            new ModEntry
            {
                Name = "Rillaboom Wood Variants",
                Slug = "rillaboom-wood-variant-cosmetics-cobblemon",
                FileName = "Wood Cosmetic Rillaboom.zip",
                DownloadUrl = "https://cdn.modrinth.com/data/eThFuBO4/versions/RPZco1mt/Wood%20Cosmetic%20Rillaboom.zip",
                ExpectedSize = 137766,
                IsDatapack = true
            },
            new ModEntry
            {
                Name = "Cobblemon Pokestops",
                Slug = "cobblemon-pokestops",
                FileName = "cobblemon-pokestops-fabric-1.9.2.jar",
                DownloadUrl = "https://cdn.modrinth.com/data/3clcaaoH/versions/oUw7DLvH/cobblemon-pokestops-fabric-1.9.2.jar",
                ExpectedSize = 466367,
                IsDatapack = false
            },
            new ModEntry
            {
                Name = "Cobblemon Expeditions",
                Slug = "cobblemon_expeditions",
                FileName = "cobblemon-expeditions-fabric-1.8.0.jar",
                DownloadUrl = "https://cdn.modrinth.com/data/HtRy1shF/versions/47CZXRC9/cobblemon-expeditions-fabric-1.8.0.jar",
                ExpectedSize = 1031127,
                IsDatapack = false
            },
            new ModEntry
            {
                Name = "W's Angry Alphas",
                Slug = "ws-angry-alphas",
                FileName = "W's Angry Alphas.zip",
                DownloadUrl = "https://cdn.modrinth.com/data/ZMIGaD0V/versions/9chEWhXr/W%27s%20Angry%20Alphas.zip",
                ExpectedSize = 4271,
                IsDatapack = true
            },
            new ModEntry
            {
                Name = "Pokebelt",
                Slug = "pokebelt-cobblemon",
                FileName = "pokebelt-0.1.0.jar",
                DownloadUrl = "https://cdn.modrinth.com/data/kgst0RnJ/versions/6BCT9c6x/pokebelt-0.1.0.jar",
                ExpectedSize = 1182692,
                IsDatapack = false
            },
            new ModEntry
            {
                Name = "Cobblemon Trainers",
                Slug = "cobblemon-trainers",
                FileName = "a-0.5.zip",
                DownloadUrl = "https://cdn.modrinth.com/data/VNLzxj2I/versions/q5Wx5WLY/a-0.5.zip",
                ExpectedSize = 303376,
                IsDatapack = true
            },
            new ModEntry
            {
                Name = "Cobblemon Trainer Pass",
                Slug = "trainer-pass",
                FileName = "Cobblemon-Trainer-Pass-Universal-1.3.5+universal.jar",
                DownloadUrl = "https://cdn.modrinth.com/data/Zly5Pzt2/versions/plwu7Uky/Cobblemon-Trainer-Pass-Universal-1.3.5%2Buniversal.jar",
                ExpectedSize = 774197,
                IsDatapack = false
            },
            new ModEntry
            {
                Name = "GeckoLib (Fabric 1.21.1)",
                Slug = "geckolib",
                FileName = "geckolib-fabric-1.21.1-4.9.3.jar",
                DownloadUrl = "https://cdn.modrinth.com/data/8BmcQJ2H/versions/tmxM7U4W/geckolib-fabric-1.21.1-4.9.3.jar",
                ExpectedSize = 664463,
                IsDatapack = false
            },
            new ModEntry
            {
                Name = "Matthiesen Core",
                Slug = "matthiesen-core",
                FileName = "matthiesen-core-fabric-1.2.10.jar",
                DownloadUrl = "https://cdn.modrinth.com/data/sqEZOmQo/versions/azvkmoed/matthiesen-core-fabric-1.2.10.jar",
                ExpectedSize = 516936,
                IsDatapack = false
            },
            new ModEntry
            {
                Name = "Forge Config API Port",
                Slug = "forge-config-api-port",
                FileName = "ForgeConfigAPIPort-v21.1.6-1.21.1-Fabric.jar",
                DownloadUrl = "https://cdn.modrinth.com/data/ohNO6lps/versions/N5qzq0XV/ForgeConfigAPIPort-v21.1.6-1.21.1-Fabric.jar",
                ExpectedSize = 633065,
                IsDatapack = false
            },
            new ModEntry
            {
                Name = "Accessories",
                Slug = "accessories",
                FileName = "accessories-fabric-1.1.0-beta.53+1.21.1.jar",
                DownloadUrl = "https://cdn.modrinth.com/data/jtmvUHXj/versions/Xlt4eWBe/accessories-fabric-1.1.0-beta.53%2B1.21.1.jar",
                ExpectedSize = 1095802,
                IsDatapack = false
            },
            new ModEntry
            {
                Name = "Lavender",
                Slug = "lavender",
                FileName = "lavender-0.1.15+1.21.jar",
                DownloadUrl = "https://cdn.modrinth.com/data/D5h9NKNI/versions/gdB0WW0x/lavender-0.1.15%2B1.21.jar",
                ExpectedSize = 539500,
                IsDatapack = false
            }
        };

        public static Task EnsureModpackAndModsAsync(string gameDir, string serverAddress, IProgress<(double percent, string status)> progress)
        {
            return EnsureModpackAndModsAsync(gameDir, serverAddress, null, progress);
        }

        public static async Task EnsureModpackAndModsAsync(
            string gameDir,
            string serverAddress,
            IReadOnlyList<RemoteModEntry>? remoteMods,
            IProgress<(double percent, string status)> progress)
        {
            Directory.CreateDirectory(gameDir);
            string modsDir = Path.Combine(gameDir, "mods");
            Directory.CreateDirectory(modsDir);

            // Determine active additional mods list
            var activeMods = new List<ModEntry>();
            if (remoteMods != null && remoteMods.Count > 0)
            {
                foreach (var r in remoteMods)
                {
                    if (!string.IsNullOrWhiteSpace(r.FileName) && !string.IsNullOrWhiteSpace(r.DownloadUrl))
                    {
                        activeMods.Add(ToModEntry(r));
                    }
                }
            }

            if (activeMods.Count == 0)
            {
                activeMods.AddRange(DefaultAdditionalMods);
            }

            // 1. Clean incompatible mod versions
            CleanIncompatibleMods(modsDir);

            // 2. Base Modpack Verification
            string indexFile = Path.Combine(gameDir, "modrinth.index.json");
            string packMarker = Path.Combine(gameDir, ".cobblemon_pack_installed");

            if (!File.Exists(packMarker) || !File.Exists(indexFile))
            {
                await InstallBaseModpackAsync(gameDir, progress);
                File.WriteAllText(packMarker, "1.8.1");
            }
            else
            {
                progress.Report((20, "Đang kiểm tra tính toàn vẹn Base Modpack..."));
                await VerifyBaseModpackAsync(gameDir, progress);
            }

            // 3. Ensure Additional Mods, Datapacks & Resourcepacks (with safe local tracking cleanup)
            progress.Report((35, $"Đang kiểm tra {activeMods.Count} mod, datapack & resourcepack bổ sung..."));
            await InstallAndVerifyAdditionalModsAsync(gameDir, activeMods, progress);

            // 4. Pre-flight check: Final sanity verification
            PreflightModCheck(gameDir, activeMods);

            progress.Report((100, "Đồng bộ toàn bộ Modpack & Mod bổ sung hoàn tất!"));
        }

        private static void CleanIncompatibleMods(string modsDir)
        {
            if (!Directory.Exists(modsDir)) return;

            string[] badPatterns = new[]
            {
                "geckolib*5.*",
                "geckolib*26.*",
                "ForgeConfigAPIPort*26.*",
                "lavender*1.21.4*",
                "lavender*1.21.2*",
                "lavender*1.21.3*"
            };

            foreach (var pattern in badPatterns)
            {
                try
                {
                    foreach (var file in Directory.GetFiles(modsDir, pattern))
                    {
                        try { File.Delete(file); } catch { }
                    }
                }
                catch { }
            }
        }

        private static async Task InstallBaseModpackAsync(string gameDir, IProgress<(double percent, string status)> progress)
        {
            progress.Report((5, "Đang tải Base Modpack Cobblemon 1.8.1..."));

            string tempPackPath = Path.Combine(gameDir, "base_pack_temp.zip");
            await DownloadFileWithProgressAsync(BaseModpackUrl, tempPackPath, 5, 20, "Tải Base Modpack mrpack", progress);

            progress.Report((20, "Đang giải nén Base Modpack..."));

            string indexDest = Path.Combine(gameDir, "modrinth.index.json");

            using (var zip = ZipFile.OpenRead(tempPackPath))
            {
                // Extract overrides
                foreach (var entry in zip.Entries)
                {
                    if (entry.FullName.StartsWith("overrides/") && !string.IsNullOrEmpty(entry.Name))
                    {
                        string relativePath = entry.FullName.Substring("overrides/".Length);
                        string destPath = Path.Combine(gameDir, relativePath);
                        string? destDir = Path.GetDirectoryName(destPath);
                        if (!string.IsNullOrEmpty(destDir)) Directory.CreateDirectory(destDir);

                        entry.ExtractToFile(destPath, true);
                    }
                }

                // Extract index file
                var indexEntry = zip.GetEntry("modrinth.index.json");
                if (indexEntry != null)
                {
                    indexEntry.ExtractToFile(indexDest, true);
                }
            }

            try { File.Delete(tempPackPath); } catch { }

            // Download files from index
            if (File.Exists(indexDest))
            {
                await VerifyBaseModpackAsync(gameDir, progress);
            }
        }

        private static async Task VerifyBaseModpackAsync(string gameDir, IProgress<(double percent, string status)> progress)
        {
            string indexDest = Path.Combine(gameDir, "modrinth.index.json");
            if (!File.Exists(indexDest)) return;

            string json = await File.ReadAllTextAsync(indexDest);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (root.TryGetProperty("files", out var filesElement) && filesElement.ValueKind == JsonValueKind.Array)
            {
                var filesList = new List<(string path, string downloadUrl, long size)>();
                foreach (var f in filesElement.EnumerateArray())
                {
                    string path = f.GetProperty("path").GetString() ?? "";
                    long size = f.TryGetProperty("fileSize", out var sz) ? sz.GetInt64() : 0;
                    var downloads = f.GetProperty("downloads");
                    if (downloads.GetArrayLength() > 0)
                    {
                        string url = downloads[0].GetString() ?? "";
                        filesList.Add((path, url, size));
                    }
                }

                // Check missing or corrupt files
                var missingList = new List<(string path, string downloadUrl, long size)>();
                foreach (var item in filesList)
                {
                    string targetPath = Path.Combine(gameDir, item.path);
                    if (!File.Exists(targetPath) || (item.size > 0 && new FileInfo(targetPath).Length != item.size))
                    {
                        missingList.Add(item);
                    }
                }

                if (missingList.Count > 0)
                {
                    int total = missingList.Count;
                    int current = 0;

                    foreach (var item in missingList)
                    {
                        current++;
                        string targetPath = Path.Combine(gameDir, item.path);
                        string? parentDir = Path.GetDirectoryName(targetPath);
                        if (!string.IsNullOrEmpty(parentDir)) Directory.CreateDirectory(parentDir);

                        double pct = 20.0 + ((double)current / total * 15.0);
                        string filename = Path.GetFileName(item.path);
                        progress.Report((pct, $"Tải mod cơ bản ({current}/{total}): {filename}"));

                        bool success = await DownloadFileWithRetryAsync(item.downloadUrl, targetPath, 3);
                        if (!success)
                        {
                            throw new InvalidOperationException($"Không thể tải file mod cơ bản '{filename}' do kết nối mạng yếu hoặc gián đoạn. Vui lòng kiểm tra lại mạng!");
                        }
                    }
                }
            }
        }

        private static async Task InstallAndVerifyAdditionalModsAsync(
            string gameDir,
            IReadOnlyList<ModEntry> modsList,
            IProgress<(double percent, string status)> progress)
        {
            string modsDir = Path.Combine(gameDir, "mods");
            string openloaderDataDir = Path.Combine(gameDir, "openloader", "data");
            string openloaderResourcesDir = Path.Combine(gameDir, "openloader", "resources");
            string datapacksDir = Path.Combine(gameDir, "datapacks");
            string resourcepacksDir = Path.Combine(gameDir, "resourcepacks");
            Directory.CreateDirectory(modsDir);
            Directory.CreateDirectory(openloaderDataDir);
            Directory.CreateDirectory(openloaderResourcesDir);
            Directory.CreateDirectory(datapacksDir);
            Directory.CreateDirectory(resourcepacksDir);

            string trackingFilePath = Path.Combine(gameDir, ".installed_additional_mods.json");
            var previouslyInstalled = new List<InstalledModRecord>();

            if (File.Exists(trackingFilePath))
            {
                try
                {
                    string json = await File.ReadAllTextAsync(trackingFilePath);
                    var parsed = JsonSerializer.Deserialize<List<InstalledModRecord>>(json);
                    if (parsed != null) previouslyInstalled = parsed;
                }
                catch { }
            }
            else
            {
                // First run with dynamic tracking: Seed tracking list with default mods currently on disk
                foreach (var defMod in DefaultAdditionalMods)
                {
                    string checkPath = defMod.IsResourcepack
                        ? Path.Combine(resourcepacksDir, defMod.FileName)
                        : (defMod.IsDatapack ? Path.Combine(openloaderDataDir, defMod.FileName) : Path.Combine(modsDir, defMod.FileName));

                    if (File.Exists(checkPath))
                    {
                        previouslyInstalled.Add(new InstalledModRecord
                        {
                            FileName = defMod.FileName,
                            IsDatapack = defMod.IsDatapack,
                            IsResourcepack = defMod.IsResourcepack
                        });
                    }
                }
            }

            // 1. Safe cleanup: ONLY remove files that were previously installed as additional mods and no longer in modsList
            var currentFileNames = new HashSet<string>(modsList.Select(m => m.FileName), StringComparer.OrdinalIgnoreCase);
            foreach (var oldRecord in previouslyInstalled)
            {
                if (!currentFileNames.Contains(oldRecord.FileName))
                {
                    try
                    {
                        if (oldRecord.IsResourcepack)
                        {
                            string p1 = Path.Combine(resourcepacksDir, oldRecord.FileName);
                            string p2 = Path.Combine(openloaderResourcesDir, oldRecord.FileName);
                            if (File.Exists(p1)) File.Delete(p1);
                            if (File.Exists(p2)) File.Delete(p2);
                        }
                        else if (oldRecord.IsDatapack)
                        {
                            string p1 = Path.Combine(openloaderDataDir, oldRecord.FileName);
                            string p2 = Path.Combine(datapacksDir, oldRecord.FileName);
                            if (File.Exists(p1)) File.Delete(p1);
                            if (File.Exists(p2)) File.Delete(p2);
                        }
                        else
                        {
                            string p = Path.Combine(modsDir, oldRecord.FileName);
                            if (File.Exists(p)) File.Delete(p);
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[ModpackService] Lỗi dọn dẹp mod/resourcepack cũ '{oldRecord.FileName}': {ex.Message}");
                    }
                }
            }

            // 2. Download missing or modified items
            int total = modsList.Count;
            int current = 0;

            foreach (var mod in modsList)
            {
                current++;
                double pct = 35.0 + ((double)current / Math.Max(total, 1) * 35.0);

                string targetPath;
                if (mod.IsResourcepack)
                {
                    targetPath = Path.Combine(resourcepacksDir, mod.FileName);
                }
                else if (mod.IsDatapack)
                {
                    targetPath = Path.Combine(openloaderDataDir, mod.FileName);
                }
                else
                {
                    targetPath = Path.Combine(modsDir, mod.FileName);
                }

                bool needsDownload = !File.Exists(targetPath) ||
                                     new FileInfo(targetPath).Length == 0 ||
                                     (mod.ExpectedSize > 0 && new FileInfo(targetPath).Length != mod.ExpectedSize);

                if (needsDownload)
                {
                    string typeLabel = mod.IsResourcepack ? "Resourcepack" : (mod.IsDatapack ? "Datapack" : "Mod");
                    progress.Report((pct, $"Đang tải {typeLabel} ({current}/{total}): {mod.Name}..."));
                    bool success = await DownloadFileWithRetryAsync(mod.DownloadUrl, targetPath, 3);
                    if (!success)
                    {
                        throw new InvalidOperationException($"Không thể tải {typeLabel} '{mod.Name}' ({mod.FileName}). Vui lòng kiểm tra lại kết nối mạng và thử lại!");
                    }
                }

                if (mod.IsResourcepack)
                {
                    string olResPath = Path.Combine(openloaderResourcesDir, mod.FileName);
                    try
                    {
                        if (!File.Exists(olResPath) || new FileInfo(olResPath).Length != new FileInfo(targetPath).Length)
                        {
                            File.Copy(targetPath, olResPath, true);
                        }
                    }
                    catch { }
                }
                else if (mod.IsDatapack)
                {
                    string dpPath = Path.Combine(datapacksDir, mod.FileName);
                    try
                    {
                        if (!File.Exists(dpPath) || new FileInfo(dpPath).Length != new FileInfo(targetPath).Length)
                        {
                            File.Copy(targetPath, dpPath, true);
                        }
                    }
                    catch { }
                }
            }

            // 3. Save new tracking state
            try
            {
                var newTracking = modsList.Select(m => new InstalledModRecord
                {
                    FileName = m.FileName,
                    IsDatapack = m.IsDatapack,
                    IsResourcepack = m.IsResourcepack
                }).ToList();
                string newJson = JsonSerializer.Serialize(newTracking, new JsonSerializerOptions { WriteIndented = true });
                await File.WriteAllTextAsync(trackingFilePath, newJson);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ModpackService] Không thể ghi file tracking mod: {ex.Message}");
            }
        }

        private static void PreflightModCheck(string gameDir, IReadOnlyList<ModEntry> modsList)
        {
            string modsDir = Path.Combine(gameDir, "mods");
            string openloaderDataDir = Path.Combine(gameDir, "openloader", "data");
            string resourcepacksDir = Path.Combine(gameDir, "resourcepacks");

            var missing = new List<string>();

            // 1. Check core cobblemon mod
            string[] cobblemonJars = Directory.Exists(modsDir)
                ? Directory.GetFiles(modsDir, "Cobblemon*.jar")
                : Array.Empty<string>();

            if (cobblemonJars.Length == 0)
            {
                missing.Add("Cobblemon Mod Core (Cobblemon-fabric-1.8.1+1.21.1.jar)");
            }

            // 2. Check each active item
            foreach (var mod in modsList)
            {
                string targetPath = mod.IsResourcepack
                    ? Path.Combine(resourcepacksDir, mod.FileName)
                    : (mod.IsDatapack ? Path.Combine(openloaderDataDir, mod.FileName) : Path.Combine(modsDir, mod.FileName));

                if (!File.Exists(targetPath) || new FileInfo(targetPath).Length == 0)
                {
                    missing.Add($"{mod.Name} ({mod.FileName})");
                }
            }

            if (missing.Count > 0)
            {
                string missingList = string.Join("\n - ", missing);
                throw new InvalidOperationException($"Phát hiện thiếu file trước khi khởi chạy game:\n - {missingList}\n\nLauncher đã dừng khởi chạy để tránh lỗi game.");
            }
        }

        private static async Task<bool> DownloadFileWithRetryAsync(string url, string destPath, int maxRetries)
        {
            string tempPath = destPath + ".tmp";
            string? dir = Path.GetDirectoryName(destPath);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                try
                {
                    using var resp = await HttpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
                    if (resp.IsSuccessStatusCode)
                    {
                        using (var src = await resp.Content.ReadAsStreamAsync())
                        using (var dst = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true))
                        {
                            await src.CopyToAsync(dst);
                        }

                        if (File.Exists(destPath)) File.Delete(destPath);
                        File.Move(tempPath, destPath);
                        return true;
                    }
                }
                catch
                {
                    try { if (File.Exists(tempPath)) File.Delete(tempPath); } catch { }
                    if (attempt == maxRetries) return false;
                    await Task.Delay(500 * attempt);
                }
            }

            return false;
        }

        private static async Task DownloadFileWithProgressAsync(
            string url,
            string destPath,
            double startPct,
            double endPct,
            string label,
            IProgress<(double percent, string status)> progress)
        {
            using var response = await HttpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();

            long totalBytes = response.Content.Headers.ContentLength ?? 100_000_000L;
            using var stream = await response.Content.ReadAsStreamAsync();
            using var fileStream = new FileStream(destPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true);

            var buffer = new byte[81920];
            long totalRead = 0;
            int read;

            while ((read = await stream.ReadAsync(buffer, 0, buffer.Length)) > 0)
            {
                await fileStream.WriteAsync(buffer, 0, read);
                totalRead += read;
                double fraction = (double)totalRead / totalBytes;
                double pct = startPct + (fraction * (endPct - startPct));
                progress.Report((pct, $"{label}: {totalRead / 1024 / 1024}MB / {totalBytes / 1024 / 1024}MB ({pct:F0}%)"));
            }
        }
    }
}
