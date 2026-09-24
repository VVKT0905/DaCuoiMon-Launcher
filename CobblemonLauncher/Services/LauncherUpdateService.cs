using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Security.Cryptography;
using System.Threading.Tasks;
using System.Windows;

namespace CobblemonLauncher.Services
{
    public class UpdateProgressInfo
    {
        public double Percent { get; set; }
        public double SpeedMBps { get; set; }
        public long BytesReceived { get; set; }
        public long TotalBytes { get; set; }
        public string StatusText { get; set; } = "";
        public bool IsCompleted { get; set; }
        public string? ErrorMessage { get; set; }
    }

    public static class LauncherUpdateService
    {
        private static readonly HttpClient HttpClient;

        static LauncherUpdateService()
        {
            var handler = new HttpClientHandler
            {
                AllowAutoRedirect = true,
                MaxAutomaticRedirections = 10
            };
            HttpClient = new HttpClient(handler);
            HttpClient.DefaultRequestHeaders.UserAgent.ParseAdd("CobblemonLauncherDotNet/2.0");
            HttpClient.Timeout = TimeSpan.FromMinutes(10);
        }

        public static string GetCurrentVersion()
        {
            try
            {
                var ver = Assembly.GetExecutingAssembly().GetName().Version;
                if (ver != null)
                {
                    return $"{ver.Major}.{ver.Minor}.{ver.Build}";
                }
            }
            catch { }
            return "2.0.0";
        }

        public static void CleanupOldFiles()
        {
            Task.Run(async () =>
            {
                await Task.Delay(1500);
                try
                {
                    string tempDir = Path.GetTempPath();
                    foreach (var file in Directory.GetFiles(tempDir, "CobblemonLauncher_Update_*.*"))
                    {
                        try { File.Delete(file); } catch { }
                    }
                    foreach (var file in Directory.GetFiles(tempDir, "update_dacuoi_*.*"))
                    {
                        try { File.Delete(file); } catch { }
                    }

                    string appDir = AppDomain.CurrentDomain.BaseDirectory;
                    foreach (var file in Directory.GetFiles(appDir, "*.tmp"))
                    {
                        if (file.Contains("CobblemonLauncher") || file.Contains(".old_"))
                        {
                            try { File.Delete(file); } catch { }
                        }
                    }
                    foreach (var file in Directory.GetFiles(appDir, "*.old*"))
                    {
                        try { File.Delete(file); } catch { }
                    }
                }
                catch { }
            });
        }

        public static bool IsNewerVersion(string remoteVersion, string currentVersion)
        {
            if (string.IsNullOrWhiteSpace(remoteVersion)) return false;

            string cleanRemote = remoteVersion.TrimStart('v', 'V').Trim();
            string cleanCurrent = currentVersion.TrimStart('v', 'V').Trim();

            if (Version.TryParse(cleanRemote, out var rVer) && Version.TryParse(cleanCurrent, out var cVer))
            {
                return rVer > cVer;
            }

            return string.Compare(cleanRemote, cleanCurrent, StringComparison.OrdinalIgnoreCase) > 0;
        }

        public static async Task<bool> DownloadAndApplyUpdateAsync(
            string downloadUrl,
            string? expectedSha256,
            IProgress<UpdateProgressInfo> progress)
        {
            if (string.IsNullOrWhiteSpace(downloadUrl))
                return false;

            string currentExePath = Process.GetCurrentProcess().MainModule?.FileName 
                ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "CobblemonLauncher.exe");
            string targetDir = Path.GetDirectoryName(currentExePath) ?? AppDomain.CurrentDomain.BaseDirectory;

            // Always write the downloaded package to %TEMP% first for guaranteed write permissions
            string tempDownloadedExe = Path.Combine(Path.GetTempPath(), $"CobblemonLauncher_Update_{Guid.NewGuid():N}.exe");

            progress.Report(new UpdateProgressInfo
            {
                Percent = 5,
                StatusText = "Đang kết nối đến máy chủ cập nhật...",
                BytesReceived = 0,
                TotalBytes = 0
            });

            try
            {
                using (var response = await HttpClient.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead))
                {
                    response.EnsureSuccessStatusCode();
                    long totalBytes = response.Content.Headers.ContentLength ?? 78_000_000L;

                    using (var stream = await response.Content.ReadAsStreamAsync())
                    using (var fileStream = new FileStream(tempDownloadedExe, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true))
                    {
                        var buffer = new byte[81920];
                        long totalRead = 0;
                        int read;
                        var stopwatch = Stopwatch.StartNew();
                        long lastSampleBytes = 0;
                        double lastSampleSeconds = 0;
                        double currentSpeedMBps = 0;

                        while ((read = await stream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                        {
                            await fileStream.WriteAsync(buffer, 0, read);
                            totalRead += read;

                            double elapsed = stopwatch.Elapsed.TotalSeconds;
                            if (elapsed - lastSampleSeconds >= 0.3)
                            {
                                long deltaBytes = totalRead - lastSampleBytes;
                                double deltaTime = elapsed - lastSampleSeconds;
                                currentSpeedMBps = (deltaBytes / 1024.0 / 1024.0) / deltaTime;
                                lastSampleBytes = totalRead;
                                lastSampleSeconds = elapsed;
                            }

                            double fraction = Math.Min(1.0, (double)totalRead / totalBytes);
                            double pct = 5.0 + (fraction * 85.0);

                            progress.Report(new UpdateProgressInfo
                            {
                                Percent = pct,
                                SpeedMBps = currentSpeedMBps,
                                BytesReceived = totalRead,
                                TotalBytes = totalBytes,
                                StatusText = $"Đang tải Launcher v2.0.0 ({totalRead / 1024 / 1024:0.#} MB / {totalBytes / 1024 / 1024:0.#} MB)"
                            });
                        }
                    }
                }

                // 2. Validate SHA-256 Checksum (if provided in remoteConfig)
                if (!string.IsNullOrWhiteSpace(expectedSha256))
                {
                    progress.Report(new UpdateProgressInfo
                    {
                        Percent = 92,
                        StatusText = "Đang kiểm tra tính toàn vẹn file (SHA-256 Checksum)..."
                    });

                    using (var sha = SHA256.Create())
                    using (var checkStream = File.OpenRead(tempDownloadedExe))
                    {
                        byte[] hashBytes = await sha.ComputeHashAsync(checkStream);
                        string computedHash = Convert.ToHexString(hashBytes).ToLowerInvariant();
                        string cleanExpected = expectedSha256.Trim().ToLowerInvariant();

                        if (!string.Equals(computedHash, cleanExpected, StringComparison.OrdinalIgnoreCase))
                        {
                            try { File.Delete(tempDownloadedExe); } catch { }
                            progress.Report(new UpdateProgressInfo
                            {
                                Percent = 0,
                                ErrorMessage = $"Lỗi kiểm tra toàn vẹn: Mã SHA-256 không khớp! Bản tải về có thể bị lỗi đường truyền.",
                                StatusText = "Cập nhật thất bại. Đã khôi phục an toàn."
                            });
                            return false;
                        }
                    }
                }

                progress.Report(new UpdateProgressInfo
                {
                    Percent = 96,
                    StatusText = "Đang áp dụng bản cập nhật nguyên tử (Atomic In-Place Swap)..."
                });

                // 3. Check if target directory requires elevation (e.g. C:\Program Files)
                bool isWritable = true;
                try
                {
                    string testFile = Path.Combine(targetDir, $".swap_test_{Guid.NewGuid():N}.tmp");
                    File.WriteAllText(testFile, "test");
                    File.Delete(testFile);
                }
                catch
                {
                    isWritable = false;
                }

                if (isWritable)
                {
                    // ==============================================================
                    // PURE ATOMIC IN-PLACE SWAP (Windows Native C# - Zero CMD / Zero lag)
                    // ==============================================================
                    string oldExePath = Path.Combine(targetDir, $"CobblemonLauncher.old_{Guid.NewGuid():N}.tmp");

                    // 1. Rename running exe to .old (NTFS allows renaming running binaries)
                    int retries = 5;
                    while (retries > 0)
                    {
                        try
                        {
                            File.Move(currentExePath, oldExePath, true);
                            break;
                        }
                        catch (IOException) when (retries > 1)
                        {
                            retries--;
                            await Task.Delay(200);
                        }
                    }

                    // 2. Move new downloaded exe into official target location
                    File.Move(tempDownloadedExe, currentExePath, true);

                    progress.Report(new UpdateProgressInfo
                    {
                        Percent = 100,
                        IsCompleted = true,
                        StatusText = "Cập nhật thành công! Đang khởi động Launcher v2.0.0..."
                    });

                    // 3. Launch the new version immediately
                    var startInfo = new ProcessStartInfo
                    {
                        FileName = currentExePath,
                        Arguments = "--cleanup-old",
                        UseShellExecute = true,
                        WorkingDirectory = targetDir
                    };
                    Process.Start(startInfo);

                    // 4. Terminate current process
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        Application.Current.Shutdown();
                    });

                    return true;
                }
                else
                {
                    // Fallback for directories requiring Administrator privileges (e.g. C:\Program Files)
                    // Uses clean background elevated script with hidden window
                    int currentPid = Process.GetCurrentProcess().Id;
                    string batPath = Path.Combine(Path.GetTempPath(), $"update_dacuoi_{Guid.NewGuid():N}.bat");

                    string batContent = $@"@echo off
chcp 65001 >nul
set TARGET=""{currentExePath}""
set NEWFILE=""{tempDownloadedExe}""
set PID={currentPid}

:WAIT_LOOP
tasklist /FI ""PID eq %PID%"" 2>NUL | find /I ""%PID%"" >NUL
if not errorlevel 1 (
    timeout /T 1 /NOBREAK >NUL
    goto WAIT_LOOP
)

timeout /T 1 /NOBREAK >NUL

set RETRIES=10
:COPY_LOOP
copy /Y %NEWFILE% %TARGET% >NUL 2>&1
if errorlevel 1 (
    set /A RETRIES-=1
    if %RETRIES% GTR 0 (
        timeout /T 1 /NOBREAK >NUL
        goto COPY_LOOP
    )
)

del /F /Q %NEWFILE% >NUL 2>&1
start """" %TARGET%
del ""%~f0"" >NUL 2>&1
";
                    await File.WriteAllTextAsync(batPath, batContent);

                    var psi = new ProcessStartInfo
                    {
                        FileName = batPath,
                        CreateNoWindow = true,
                        UseShellExecute = true,
                        WindowStyle = ProcessWindowStyle.Hidden,
                        Verb = "runas"
                    };

                    Process.Start(psi);

                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        Application.Current.Shutdown();
                    });

                    return true;
                }
            }
            catch (Exception ex)
            {
                try { File.Delete(tempDownloadedExe); } catch { }
                progress.Report(new UpdateProgressInfo
                {
                    Percent = 0,
                    ErrorMessage = $"Không thể tải bản cập nhật: {ex.Message}",
                    StatusText = "Cập nhật gián đoạn. Tiếp tục vào Launcher..."
                });
                return false;
            }
        }
    }
}
