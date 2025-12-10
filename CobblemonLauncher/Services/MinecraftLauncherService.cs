using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace CobblemonLauncher.Services
{
    public class MinecraftLauncherService
    {
        private static readonly HttpClient HttpClient = new HttpClient();
        private const string MinecraftVersion = "1.21.1";
        private const string VersionPackageUrl = "https://piston-meta.mojang.com/v1/packages/eb17d0d5933eec4a056c9f649db1e33c69622785/1.21.1.json";
        private const string FabricProfileUrl = "https://meta.fabricmc.net/v2/versions/loader/1.21.1/0.19.5/profile/json";

        public static event Action<string>? OnLogOutput;

        public static async Task<Process?> LaunchAsync(
            string javaPath,
            string gameDir,
            int ramGb,
            PlayerSession session,
            string serverAddress,
            bool directConnect,
            IProgress<(double percent, string status)> progress)
        {
            // 1. Ensure Minecraft 1.21.1 Client & Vanilla Libraries
            progress.Report((10, "Đang kiểm tra Minecraft 1.21.1 Client & Libraries..."));
            var (clientJarPath, vanillaLibPaths) = await EnsureVanillaAsync(gameDir, progress);

            // 2. Ensure Fabric Loader Libraries
            progress.Report((50, "Đang kiểm tra Fabric Loader Libraries..."));
            var fabricLibPaths = await EnsureFabricAsync(gameDir, progress);

            // 3. Ensure Assets
            progress.Report((75, "Đang kiểm tra Assets..."));
            string assetsDir = await EnsureAssetsAsync(gameDir, progress);

            // 4. Build Classpath
            progress.Report((90, "Đang cấu hình Classpath & Tham số khởi chạy..."));
            var classpathList = new List<string>();
            classpathList.AddRange(fabricLibPaths);
            classpathList.AddRange(vanillaLibPaths);
            classpathList.Add(clientJarPath);

            string classpath = string.Join(";", classpathList);

            string nativesDir = Path.Combine(gameDir, "bin", "natives");
            Directory.CreateDirectory(nativesDir);

            string javaBinDir = Path.GetDirectoryName(javaPath) ?? "";
            string javaHome = Path.GetDirectoryName(javaBinDir) ?? "";

            // 5. Prepare Arguments
            var jvmArgs = new List<string>
            {
                $"-Xmx{ramGb}G",
                "-Xms1G",
                $"-Djava.library.path=\"{nativesDir};{javaBinDir}\"",
                $"-Dorg.lwjgl.librarypath=\"{nativesDir}\"",
                "-DFabricMcEmu=net.minecraft.client.main.Main",
                "-cp",
                $"\"{classpath}\"",
                "net.fabricmc.loader.impl.launch.knot.KnotClient"
            };

            var gameArgs = new List<string>
            {
                $"--username \"{session.Username}\"",
                "--version 1.21.1-fabric",
                $"--gameDir \"{gameDir}\"",
                $"--assetsDir \"{assetsDir}\"",
                "--assetIndex 17",
                $"--uuid \"{session.Uuid}\"",
                $"--accessToken \"{session.AccessToken}\"",
                $"--userType \"{session.UserType}\""
            };

            if (directConnect && !string.IsNullOrWhiteSpace(serverAddress))
            {
                string cleanAddr = serverAddress.Trim();
                gameArgs.Add($"--quickPlayMultiplayer \"{cleanAddr}\"");
                if (cleanAddr.Contains(":"))
                {
                    var parts = cleanAddr.Split(':');
                    gameArgs.Add($"--server {parts[0]} --port {parts[1]}");
                }
                else
                {
                    gameArgs.Add($"--server {cleanAddr} --port 25565");
                }
            }

            string fullCommandLine = $"{string.Join(" ", jvmArgs)} {string.Join(" ", gameArgs)}";

            progress.Report((100, "Đang khởi chạy Minecraft Cobblemon..."));
            OnLogOutput?.Invoke($"Khởi chạy Java: {javaPath}");
            OnLogOutput?.Invoke($"Thư mục game: {gameDir}");
            OnLogOutput?.Invoke($"Tài khoản: {session.Username} (UUID: {session.Uuid})");

            var psi = new ProcessStartInfo
            {
                FileName = javaPath,
                Arguments = fullCommandLine,
                WorkingDirectory = gameDir,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = false
            };

            if (!string.IsNullOrEmpty(javaBinDir))
            {
                string existingPath = Environment.GetEnvironmentVariable("PATH") ?? "";
                psi.EnvironmentVariables["PATH"] = $"{javaBinDir};{existingPath}";
                if (!string.IsNullOrEmpty(javaHome))
                {
                    psi.EnvironmentVariables["JAVA_HOME"] = javaHome;
                }
            }

            var proc = new Process { StartInfo = psi };
            proc.OutputDataReceived += (s, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    OnLogOutput?.Invoke(e.Data);
                }
            };
            proc.ErrorDataReceived += (s, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    OnLogOutput?.Invoke("[LỖI] " + e.Data);
                }
            };

            proc.Start();
            proc.BeginOutputReadLine();
            proc.BeginErrorReadLine();

            return proc;
        }

        private static async Task<(string clientJar, List<string> libPaths)> EnsureVanillaAsync(
            string gameDir,
            IProgress<(double percent, string status)> progress)
        {
            string versionsDir = Path.Combine(gameDir, "versions", MinecraftVersion);
            Directory.CreateDirectory(versionsDir);
            string clientJar = Path.Combine(versionsDir, $"{MinecraftVersion}.jar");

            string json = await HttpClient.GetStringAsync(VersionPackageUrl);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            // Download client jar
            var clientObj = root.GetProperty("downloads").GetProperty("client");
            string clientUrl = clientObj.GetProperty("url").GetString()!;
            long clientSize = clientObj.GetProperty("size").GetInt64();

            if (!File.Exists(clientJar) || new FileInfo(clientJar).Length != clientSize)
            {
                progress.Report((15, "Đang tải Minecraft 1.21.1 Client JAR..."));
                using var stream = await HttpClient.GetStreamAsync(clientUrl);
                using var fs = new FileStream(clientJar, FileMode.Create, FileAccess.Write, FileShare.None);
                await stream.CopyToAsync(fs);
            }

            // Download vanilla libraries
            string libRoot = Path.Combine(gameDir, "libraries");
            Directory.CreateDirectory(libRoot);

            var libraries = root.GetProperty("libraries");
            var libPaths = new ConcurrentBag<string>();
            var downloadQueue = new List<(string url, string path, long size)>();

            foreach (var lib in libraries.EnumerateArray())
            {
                if (!ShouldDownloadLibrary(lib)) continue;

                if (lib.TryGetProperty("downloads", out var dl) && dl.TryGetProperty("artifact", out var artifact))
                {
                    string pathRel = artifact.GetProperty("path").GetString()!;
                    string url = artifact.GetProperty("url").GetString()!;
                    long size = artifact.GetProperty("size").GetInt64();
                    string localPath = Path.Combine(libRoot, pathRel);

                    libPaths.Add(localPath);

                    if (!File.Exists(localPath) || new FileInfo(localPath).Length != size)
                    {
                        downloadQueue.Add((url, localPath, size));
                    }
                }
            }

            // Download missing libraries concurrently
            int totalDl = downloadQueue.Count;
            int done = 0;
            if (totalDl > 0)
            {
                var semaphore = new SemaphoreSlim(8);
                var tasks = new List<Task>();

                foreach (var item in downloadQueue)
                {
                    tasks.Add(Task.Run(async () =>
                    {
                        await semaphore.WaitAsync();
                        try
                        {
                            string? dir = Path.GetDirectoryName(item.path);
                            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

                            using var resp = await HttpClient.GetAsync(item.url);
                            if (resp.IsSuccessStatusCode)
                            {
                                using var fs = new FileStream(item.path, FileMode.Create, FileAccess.Write, FileShare.None);
                                await resp.Content.CopyToAsync(fs);
                            }
                        }
                        catch { }
                        finally
                        {
                            int completed = Interlocked.Increment(ref done);
                            double pct = 15.0 + ((double)completed / totalDl * 30.0);
                            progress.Report((pct, $"Tải thư viện Minecraft ({completed}/{totalDl})..."));
                            semaphore.Release();
                        }
                    }));
                }

                await Task.WhenAll(tasks);
            }

            return (clientJar, new List<string>(libPaths));
        }

        private static async Task<List<string>> EnsureFabricAsync(string gameDir, IProgress<(double percent, string status)> progress)
        {
            string libRoot = Path.Combine(gameDir, "libraries");
            Directory.CreateDirectory(libRoot);

            string json = await HttpClient.GetStringAsync(FabricProfileUrl);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var fabricLibs = root.GetProperty("libraries");
            var libPaths = new List<string>();

            foreach (var lib in fabricLibs.EnumerateArray())
            {
                string name = lib.GetProperty("name").GetString()!;
                string urlBase = lib.GetProperty("url").GetString()!;

                // Convert maven coordinate (e.g. net.fabricmc:fabric-loader:0.19.5) to relative path
                string relPath = MavenToPath(name);
                string fullUrl = urlBase.TrimEnd('/') + "/" + relPath;
                string localPath = Path.Combine(libRoot, relPath);

                libPaths.Add(localPath);

                if (!File.Exists(localPath))
                {
                    string? dir = Path.GetDirectoryName(localPath);
                    if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

                    for (int attempt = 1; attempt <= 3; attempt++)
                    {
                        try
                        {
                            using var resp = await HttpClient.GetAsync(fullUrl);
                            if (resp.IsSuccessStatusCode)
                            {
                                using var fs = new FileStream(localPath, FileMode.Create, FileAccess.Write, FileShare.None);
                                await resp.Content.CopyToAsync(fs);
                                break;
                            }
                        }
                        catch (Exception ex)
                        {
                            if (attempt == 3)
                            {
                                Console.WriteLine($"Fabric lib error {name}: {ex.Message}");
                            }
                            await Task.Delay(300 * attempt);
                        }
                    }
                }
            }

            return libPaths;
        }

        private static async Task<string> EnsureAssetsAsync(string gameDir, IProgress<(double percent, string status)> progress)
        {
            string assetsDir = Path.Combine(gameDir, "assets");
            string indexesDir = Path.Combine(assetsDir, "indexes");
            string objectsDir = Path.Combine(assetsDir, "objects");
            Directory.CreateDirectory(indexesDir);
            Directory.CreateDirectory(objectsDir);

            string indexFile = Path.Combine(indexesDir, "17.json");
            string indexUrl = "https://piston-meta.mojang.com/v1/packages/de573f83da62843433ec9951c66feec7ed0a60a1/17.json";

            if (!File.Exists(indexFile))
            {
                using var resp = await HttpClient.GetAsync(indexUrl);
                if (resp.IsSuccessStatusCode)
                {
                    using var fs = new FileStream(indexFile, FileMode.Create, FileAccess.Write, FileShare.None);
                    await resp.Content.CopyToAsync(fs);
                }
            }

            // Ensure all assets (including sounds, music, block breaking, environment, textures, icons, fonts)
            if (File.Exists(indexFile))
            {
                try
                {
                    string json = await File.ReadAllTextAsync(indexFile);
                    using var doc = JsonDocument.Parse(json);
                    if (doc.RootElement.TryGetProperty("objects", out var objects))
                    {
                        var downloadList = new List<(string sub, string hash, long size)>();
                        foreach (var prop in objects.EnumerateObject())
                        {
                            string hash = prop.Value.GetProperty("hash").GetString()!;
                            long size = prop.Value.TryGetProperty("size", out var s) ? s.GetInt64() : 0;
                            string sub = hash.Substring(0, 2);
                            string target = Path.Combine(objectsDir, sub, hash);
                            if (!File.Exists(target) || (size > 0 && new FileInfo(target).Length != size))
                            {
                                downloadList.Add((sub, hash, size));
                            }
                        }

                        if (downloadList.Count > 0)
                        {
                            int done = 0;
                            int total = downloadList.Count;
                            var sem = new SemaphoreSlim(20);
                            var tasks = new List<Task>();
                            foreach (var item in downloadList)
                            {
                                tasks.Add(Task.Run(async () =>
                                {
                                    await sem.WaitAsync();
                                    try
                                    {
                                        string dir = Path.Combine(objectsDir, item.sub);
                                        Directory.CreateDirectory(dir);
                                        string dest = Path.Combine(dir, item.hash);
                                        string tempDest = dest + ".tmp";
                                        string url = $"https://resources.download.minecraft.net/{item.sub}/{item.hash}";

                                        for (int attempt = 1; attempt <= 3; attempt++)
                                        {
                                            try
                                            {
                                                using var resp = await HttpClient.GetAsync(url);
                                                if (resp.IsSuccessStatusCode)
                                                {
                                                    using (var fs = new FileStream(tempDest, FileMode.Create, FileAccess.Write, FileShare.None))
                                                    {
                                                        await resp.Content.CopyToAsync(fs);
                                                    }
                                                    if (File.Exists(dest)) File.Delete(dest);
                                                    File.Move(tempDest, dest);
                                                    break;
                                                }
                                            }
                                            catch
                                            {
                                                try { if (File.Exists(tempDest)) File.Delete(tempDest); } catch { }
                                                if (attempt == 3) throw;
                                                await Task.Delay(300 * attempt);
                                            }
                                        }
                                    }
                                    catch { }
                                    finally
                                    {
                                        int c = Interlocked.Increment(ref done);
                                        if (c % 25 == 0 || c == total)
                                        {
                                            double pct = 70.0 + ((double)c / total * 18.0);
                                            progress.Report((pct, $"Tải âm thanh & tài nguyên game ({c}/{total})..."));
                                        }
                                        sem.Release();
                                    }
                                }));
                            }
                            await Task.WhenAll(tasks);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Asset error: {ex.Message}");
                }
            }

            return assetsDir;
        }

        private static bool ShouldDownloadLibrary(JsonElement lib)
        {
            if (!lib.TryGetProperty("rules", out var rules)) return true;

            bool allow = false;
            foreach (var rule in rules.EnumerateArray())
            {
                string action = rule.GetProperty("action").GetString()!;
                if (rule.TryGetProperty("os", out var os))
                {
                    string osName = os.GetProperty("name").GetString()!;
                    if (osName == "windows")
                    {
                        allow = (action == "allow");
                    }
                }
                else
                {
                    allow = (action == "allow");
                }
            }

            return allow;
        }

        private static string MavenToPath(string coordinate)
        {
            // Format: group:artifact:version or group:artifact:version:classifier
            var parts = coordinate.Split(':');
            string group = parts[0].Replace('.', '/');
            string artifact = parts[1];
            string version = parts[2];
            string classifier = parts.Length > 3 ? $"-{parts[3]}" : "";

            return $"{group}/{artifact}/{version}/{artifact}-{version}{classifier}.jar";
        }
    }
}
