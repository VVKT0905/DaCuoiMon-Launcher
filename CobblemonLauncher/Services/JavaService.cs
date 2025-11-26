using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Threading.Tasks;

namespace CobblemonLauncher.Services
{
    public class JavaService
    {
        private static readonly HttpClient HttpClient = new HttpClient();

        public static bool IsAscii(string value)
        {
            foreach (char c in value)
            {
                if (c > 127) return false;
            }
            return true;
        }

        public static string GetSafeRuntimeDir(string? gameDir = null)
        {
            if (!string.IsNullOrEmpty(gameDir))
            {
                string? parent = Directory.GetParent(gameDir)?.FullName;
                if (!string.IsNullOrEmpty(parent) && IsAscii(parent))
                {
                    string localRuntime = Path.Combine(parent, "runtime", "java-21");
                    Directory.CreateDirectory(localRuntime);
                    return localRuntime;
                }
            }

            string baseAppDir = AppDomain.CurrentDomain.BaseDirectory;
            if (IsAscii(baseAppDir))
            {
                string localRuntime = Path.Combine(baseAppDir, "data", "runtime", "java-21");
                Directory.CreateDirectory(localRuntime);
                return localRuntime;
            }

            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "CobblemonLauncher",
                "runtime",
                "java-21"
            );
        }

        public static bool TestJavaExecutable(string javawPath)
        {
            try
            {
                string dir = Path.GetDirectoryName(javawPath) ?? "";
                string javaExe = Path.Combine(dir, "java.exe");
                if (!File.Exists(javaExe)) javaExe = javawPath;

                var psi = new ProcessStartInfo
                {
                    FileName = javaExe,
                    Arguments = "-version",
                    RedirectStandardError = true,
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                // Add bin to PATH for safety
                string existingPath = Environment.GetEnvironmentVariable("PATH") ?? "";
                psi.EnvironmentVariables["PATH"] = $"{dir};{existingPath}";

                using var proc = Process.Start(psi);
                if (proc != null)
                {
                    string output = proc.StandardError.ReadToEnd() + " " + proc.StandardOutput.ReadToEnd();
                    proc.WaitForExit(3000);
                    if (proc.ExitCode == 0 && (output.Contains("21.") || output.Contains("version 21")))
                    {
                        return true;
                    }
                }
            }
            catch { }
            return false;
        }

        public static string? FindSystemJava21()
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "java",
                    Arguments = "-version",
                    RedirectStandardError = true,
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var proc = Process.Start(psi);
                if (proc != null)
                {
                    string output = proc.StandardError.ReadToEnd() + " " + proc.StandardOutput.ReadToEnd();
                    proc.WaitForExit(3000);
                    if (proc.ExitCode == 0 && (output.Contains("\"21.") || output.Contains("version 21.")))
                    {
                        return "java";
                    }
                }
            }
            catch { }

            // Check standard JAVA_HOME
            string? javaHome = Environment.GetEnvironmentVariable("JAVA_HOME");
            if (!string.IsNullOrWhiteSpace(javaHome))
            {
                string javawPath = Path.Combine(javaHome, "bin", "javaw.exe");
                if (File.Exists(javawPath) && TestJavaExecutable(javawPath))
                {
                    return javawPath;
                }
            }

            return null;
        }

        public static string? FindPortableJava(string gameDir)
        {
            string[] checkDirs = new[]
            {
                GetSafeRuntimeDir(gameDir),
                Path.Combine(gameDir, "runtime", "java-21"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CobblemonLauncher", "runtime", "java-21")
            };

            foreach (var runtimeDir in checkDirs)
            {
                if (Directory.Exists(runtimeDir))
                {
                    // Direct check
                    string direct = Path.Combine(runtimeDir, "bin", "javaw.exe");
                    if (File.Exists(direct) && TestJavaExecutable(direct)) return direct;

                    // Check subfolders (e.g. jdk-21.0.x)
                    var subDirs = Directory.GetDirectories(runtimeDir);
                    foreach (var dir in subDirs)
                    {
                        string candidate = Path.Combine(dir, "bin", "javaw.exe");
                        if (File.Exists(candidate) && TestJavaExecutable(candidate)) return candidate;
                    }
                }
            }

            return null;
        }

        public static async Task<string> EnsureJava21Async(string gameDir, IProgress<(double percent, string status)> progress)
        {
            // 1. Check portable java first (verified working)
            string? portable = FindPortableJava(gameDir);
            if (!string.IsNullOrEmpty(portable))
            {
                progress.Report((100, "Đã tìm thấy Java 21 Portable!"));
                return portable;
            }

            // 2. Check system java 21
            string? systemJava = FindSystemJava21();
            if (!string.IsNullOrEmpty(systemJava))
            {
                progress.Report((100, "Đã tìm thấy Java 21 hệ thống!"));
                return systemJava;
            }

            // 3. Download Adoptium OpenJDK 21 portable zip into local app runtime directory
            progress.Report((0, "Đang chuẩn bị tải Java 21 Portable (Adoptium OpenJDK 21)..."));

            string downloadUrl = "https://api.adoptium.net/v3/binary/latest/21/ga/windows/x64/jdk/hotspot/normal/eclipse";
            string runtimeDir = GetSafeRuntimeDir(gameDir);
            Directory.CreateDirectory(runtimeDir);

            string zipPath = Path.Combine(runtimeDir, "java21_temp.zip");

            using (var response = await HttpClient.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead))
            {
                response.EnsureSuccessStatusCode();
                long totalBytes = response.Content.Headers.ContentLength ?? 180_000_000L;

                using var stream = await response.Content.ReadAsStreamAsync();
                using var fileStream = new FileStream(zipPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true);

                var buffer = new byte[81920];
                long totalRead = 0;
                int read;

                while ((read = await stream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                {
                    await fileStream.WriteAsync(buffer, 0, read);
                    totalRead += read;
                    double pct = (double)totalRead / totalBytes * 100.0;
                    progress.Report((Math.Min(95.0, pct), $"Đang tải Java 21 Portable: {totalRead / 1024 / 1024}MB / {totalBytes / 1024 / 1024}MB ({pct:F0}%)"));
                }
            }

            progress.Report((96, "Đang giải nén Java 21 Portable..."));

            await Task.Run(() =>
            {
                ZipFile.ExtractToDirectory(zipPath, runtimeDir, true);
                if (File.Exists(zipPath))
                {
                    try { File.Delete(zipPath); } catch { }
                }
            });

            string? finalJava = FindPortableJava(gameDir);
            if (string.IsNullOrEmpty(finalJava))
            {
                throw new FileNotFoundException("Không tìm thấy javaw.exe sau khi giải nén Java 21!");
            }

            progress.Report((100, "Cài đặt Java 21 Portable thành công!"));
            return finalJava;
        }
    }
}
