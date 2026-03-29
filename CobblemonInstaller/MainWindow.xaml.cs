using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using Microsoft.Win32;

namespace CobblemonInstaller
{
    public partial class MainWindow : Window
    {
        private bool _isUninstallMode = false;
        private string _targetInstallDir = "";

        public MainWindow()
        {
            InitializeComponent();

            string[] args = Environment.GetCommandLineArgs();
            string exeName = Process.GetCurrentProcess().ProcessName;

            if (exeName.Contains("uninstall", StringComparison.OrdinalIgnoreCase) ||
                (args.Length > 1 && args[1].Equals("--uninstall", StringComparison.OrdinalIgnoreCase)))
            {
                _isUninstallMode = true;
            }
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            LoadMascot();

            if (_isUninstallMode)
            {
                PanelInstall.Visibility = Visibility.Collapsed;
                PanelUninstall.Visibility = Visibility.Visible;
                return;
            }

            // Default: %LocalAppData%\DaCuoiMon (e.g. C:\Users\<Username>\AppData\Local\DaCuoiMon)
            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string defaultPath = Path.Combine(localAppData, "DaCuoiMon");
            TxtInstallDir.Text = defaultPath;
        }

        private void LoadMascot()
        {
            try
            {
                var uri = new Uri("pack://application:,,,/Assets/mascot.png", UriKind.Absolute);
                var bmp = new BitmapImage(uri);
                ImgMascot.Source = bmp;
                return;
            }
            catch { }

            try
            {
                var asm = Assembly.GetExecutingAssembly();
                using var stream = asm.GetManifestResourceStream("CobblemonInstaller.Assets.mascot.png")
                                ?? asm.GetManifestResourceStream("CobblemonInstaller.mascot.png");
                if (stream != null)
                {
                    var bmp = new BitmapImage();
                    bmp.BeginInit();
                    bmp.StreamSource = stream;
                    bmp.CacheOption = BitmapCacheOption.OnLoad;
                    bmp.EndInit();
                    ImgMascot.Source = bmp;
                    return;
                }
            }
            catch { }

            try
            {
                string mascotPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "mascot.png");
                if (File.Exists(mascotPath))
                {
                    var bmp = new BitmapImage(new Uri(mascotPath, UriKind.Absolute));
                    ImgMascot.Source = bmp;
                }
            }
            catch { }
        }

        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                DragMove();
            }
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void BtnBrowse_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var dialog = new OpenFolderDialog
                {
                    Title = "Chọn thư mục cài đặt Đá Cuội Mon",
                    InitialDirectory = TxtInstallDir.Text
                };

                if (dialog.ShowDialog() == true)
                {
                    TxtInstallDir.Text = dialog.FolderName;
                }
            }
            catch
            {
                // Fallback
            }
        }

        private async void BtnInstall_Click(object sender, RoutedEventArgs e)
        {
            string targetDir = TxtInstallDir.Text.Trim();
            if (string.IsNullOrWhiteSpace(targetDir))
            {
                MessageBox.Show("Vui lòng chọn thư mục cài đặt hợp lệ!", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Close any running CobblemonLauncher instances so file locks are freed
            try
            {
                foreach (var proc in Process.GetProcessesByName("CobblemonLauncher"))
                {
                    try
                    {
                        proc.Kill();
                        proc.WaitForExit(3000);
                    }
                    catch { }
                }
            }
            catch { }

            _targetInstallDir = targetDir;
            PanelInstall.Visibility = Visibility.Collapsed;
            PanelProgress.Visibility = Visibility.Visible;

            bool createDesktop = ChkDesktopShortcut.IsChecked ?? true;
            bool createStartMenu = ChkStartMenuShortcut.IsChecked ?? true;

            try
            {
                await Task.Run(() =>
                {
                    DoInstall(targetDir, createDesktop, createStartMenu);
                });

                PanelProgress.Visibility = Visibility.Collapsed;
                PanelCompleted.Visibility = Visibility.Visible;
            }
            catch (Exception ex)
            {
                PanelProgress.Visibility = Visibility.Collapsed;
                PanelInstall.Visibility = Visibility.Visible;
                MessageBox.Show($"Đã xảy ra sự cố khi cài đặt:\n{ex.Message}\n\nVui lòng thử lại hoặc chọn thư mục cài đặt khác!", "Lỗi Cài Đặt", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void DoInstall(string targetDir, bool createDesktop, bool createStartMenu)
        {
            UpdateProgress(10, "Đang khởi tạo thư mục cài đặt...");
            if (!Directory.Exists(targetDir))
            {
                Directory.CreateDirectory(targetDir);
            }

            // Grant write access on targetDir to Users so game data, mods, and Java can write smoothly
            GrantDirectoryAccess(targetDir);

            UpdateProgress(30, "Đang giải nén bộ cài Cobblemon Launcher...");
            var assembly = Assembly.GetExecutingAssembly();
            using (var stream = assembly.GetManifestResourceStream("CobblemonInstaller.payload.zip"))
            {
                if (stream == null)
                {
                    throw new FileNotFoundException("Không tìm thấy dữ liệu bộ cài (payload.zip) trong gói thực thi!");
                }

                string tempZip = Path.Combine(Path.GetTempPath(), $"DaCuoiMon_Setup_{Guid.NewGuid():N}.zip");
                using (var fs = new FileStream(tempZip, FileMode.Create, FileAccess.Write))
                {
                    stream.CopyTo(fs);
                }

                // If a file is temporarily locked, retry up to 3 times
                int retries = 3;
                while (retries > 0)
                {
                    try
                    {
                        ZipFile.ExtractToDirectory(tempZip, targetDir, overwriteFiles: true);
                        break;
                    }
                    catch (IOException) when (retries > 1)
                    {
                        retries--;
                        System.Threading.Thread.Sleep(1000);
                    }
                }

                try { File.Delete(tempZip); } catch { }

                // Clean up any legacy instance folder if user upgrades over an older install
                string legacyInstanceDir = Path.Combine(targetDir, "instance");
                if (Directory.Exists(legacyInstanceDir))
                {
                    try { Directory.Delete(legacyInstanceDir, true); } catch { }
                }

                // Enforce YSM model blacklist and remove builtin folder
                try
                {
                    string ysmDir = Path.Combine(targetDir, "data", "instance", "config", "yes_steve_model");
                    if (!Directory.Exists(ysmDir)) Directory.CreateDirectory(ysmDir);
                    File.WriteAllText(Path.Combine(ysmDir, "blacklist.txt"), ".*\n");
                    string builtinDir = Path.Combine(ysmDir, "builtin");
                    if (Directory.Exists(builtinDir)) Directory.Delete(builtinDir, true);
                }
                catch { }
            }

            // Migrate user data from legacy installation (e.g. C:\Program Files\DaCuoiMon) if installing to a new location
            try
            {
                string legacyDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "DaCuoiMon");
                if (!string.Equals(targetDir, legacyDir, StringComparison.OrdinalIgnoreCase) && Directory.Exists(legacyDir))
                {
                    UpdateProgress(65, "Đang đồng bộ dữ liệu tài khoản từ bản cũ...");
                    // 1. Copy config.json
                    string oldConfig = Path.Combine(legacyDir, "config.json");
                    string newConfig = Path.Combine(targetDir, "config.json");
                    if (File.Exists(oldConfig) && !File.Exists(newConfig))
                    {
                        File.Copy(oldConfig, newConfig, overwrite: true);
                    }

                    // 2. Migrate player saves/screenshots if any
                    string oldInstance = Path.Combine(legacyDir, "data", "instance");
                    string newInstance = Path.Combine(targetDir, "data", "instance");
                    if (Directory.Exists(oldInstance))
                    {
                        foreach (var sub in new[] { "saves", "screenshots" })
                        {
                            string oldSub = Path.Combine(oldInstance, sub);
                            string newSub = Path.Combine(newInstance, sub);
                            if (Directory.Exists(oldSub) && !Directory.Exists(newSub))
                            {
                                try
                                {
                                    Directory.CreateDirectory(newSub);
                                    foreach (var file in Directory.GetFiles(oldSub, "*", SearchOption.AllDirectories))
                                    {
                                        string rel = Path.GetRelativePath(oldSub, file);
                                        string dest = Path.Combine(newSub, rel);
                                        Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
                                        File.Copy(file, dest, true);
                                    }
                                }
                                catch { }
                            }
                        }
                    }
                }
            }
            catch { }

            UpdateProgress(70, "Đang tạo trình gỡ cài đặt...");
            string currentExe = Process.GetCurrentProcess().MainModule?.FileName ?? "";
            string uninstallerPath = Path.Combine(targetDir, "Uninstall.exe");
            if (File.Exists(currentExe) && !string.Equals(currentExe, uninstallerPath, StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    File.Copy(currentExe, uninstallerPath, overwrite: true);
                }
                catch { }
            }

            UpdateProgress(85, "Đang tạo lối tắt Desktop & Start Menu...");
            string launcherExe = Path.Combine(targetDir, "CobblemonLauncher.exe");
            string icoPath = Path.Combine(targetDir, "app.ico");
            if (!File.Exists(icoPath)) icoPath = launcherExe;

            if (createDesktop)
            {
                // Create in User Desktop
                try
                {
                    string userDesktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                    if (!string.IsNullOrEmpty(userDesktop))
                    {
                        string lnk = Path.Combine(userDesktop, "Đá Cuội Mon.lnk");
                        NativeShortcut.Create(lnk, launcherExe, targetDir, "Khởi chạy Đá Cuội Mon", icoPath);
                    }
                }
                catch { }

                // Create in Common/Public Desktop (ensures visibility when elevated)
                try
                {
                    string commonDesktop = Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory);
                    if (!string.IsNullOrEmpty(commonDesktop))
                    {
                        string lnk = Path.Combine(commonDesktop, "Đá Cuội Mon.lnk");
                        NativeShortcut.Create(lnk, launcherExe, targetDir, "Khởi chạy Đá Cuội Mon", icoPath);
                    }
                }
                catch { }
            }

            if (createStartMenu)
            {
                // Create in User Start Menu Programs
                try
                {
                    string userSm = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), "Đá Cuội Mon");
                    Directory.CreateDirectory(userSm);
                    NativeShortcut.Create(Path.Combine(userSm, "Đá Cuội Mon.lnk"), launcherExe, targetDir, "Khởi chạy Đá Cuội Mon", icoPath);
                    NativeShortcut.Create(Path.Combine(userSm, "Gỡ cài đặt Đá Cuội Mon.lnk"), uninstallerPath, targetDir, "Gỡ cài đặt Đá Cuội Mon", uninstallerPath);
                }
                catch { }

                // Create in Common Start Menu Programs
                try
                {
                    string commonSm = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms), "Đá Cuội Mon");
                    Directory.CreateDirectory(commonSm);
                    NativeShortcut.Create(Path.Combine(commonSm, "Đá Cuội Mon.lnk"), launcherExe, targetDir, "Khởi chạy Đá Cuội Mon", icoPath);
                    NativeShortcut.Create(Path.Combine(commonSm, "Gỡ cài đặt Đá Cuội Mon.lnk"), uninstallerPath, targetDir, "Gỡ cài đặt Đá Cuội Mon", uninstallerPath);
                }
                catch { }
            }

            // Register in Windows Add/Remove Programs (Registry)
            RegisterUninstall(targetDir, launcherExe, uninstallerPath);

            // Re-apply write permissions to newly extracted files and data directory
            GrantDirectoryAccess(targetDir);

            UpdateProgress(100, "Hoàn tất cài đặt!");
        }

        private void GrantDirectoryAccess(string folderPath)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "icacls.exe",
                    Arguments = $"\"{folderPath}\" /grant *S-1-5-32-545:(OI)(CI)F /T /Q",
                    CreateNoWindow = true,
                    UseShellExecute = false
                };
                using var proc = Process.Start(psi);
                proc?.WaitForExit(3000);
            }
            catch { }
        }

        private void UpdateProgress(double percent, string status)
        {
            Dispatcher.Invoke(() =>
            {
                ProgressBarInstall.Value = percent;
                TxtProgressStatus.Text = status;
            });
        }

        private void RegisterUninstall(string installDir, string mainExe, string uninstallerExe)
        {
            try
            {
                using var key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall\DaCuoiMon");
                if (key != null)
                {
                    key.SetValue("DisplayName", "Đá Cuội Mon (Cobblemon Launcher)");
                    key.SetValue("DisplayIcon", mainExe);
                    key.SetValue("DisplayVersion", "1.8.1");
                    key.SetValue("Publisher", "Đá Cuội Mon");
                    key.SetValue("InstallLocation", installDir);
                    key.SetValue("UninstallString", $"\"{uninstallerExe}\" --uninstall");
                    key.SetValue("QuietUninstallString", $"\"{uninstallerExe}\" --uninstall /quiet");
                    key.SetValue("NoModify", 1, RegistryValueKind.DWord);
                    key.SetValue("NoRepair", 1, RegistryValueKind.DWord);
                }
            }
            catch { }
        }

        private void BtnFinish_Click(object sender, RoutedEventArgs e)
        {
            if (ChkLaunchNow.IsChecked == true && !string.IsNullOrEmpty(_targetInstallDir))
            {
                string launcherExe = Path.Combine(_targetInstallDir, "CobblemonLauncher.exe");
                if (File.Exists(launcherExe))
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = launcherExe,
                        WorkingDirectory = _targetInstallDir,
                        UseShellExecute = true
                    });
                }
            }
            Close();
        }

        private async void BtnDoUninstall_Click(object sender, RoutedEventArgs e)
        {
            BtnDoUninstall.IsEnabled = false;

            await Task.Run(() =>
            {
                // 1. Close any running CobblemonLauncher instances
                foreach (var p in Process.GetProcessesByName("CobblemonLauncher"))
                {
                    try { p.Kill(); } catch { }
                }

                // 2. Remove Shortcuts from User Desktop & Common Desktop
                try
                {
                    string userDesktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                    string lnk1 = Path.Combine(userDesktop, "Đá Cuội Mon.lnk");
                    if (File.Exists(lnk1)) File.Delete(lnk1);

                    string commonDesktop = Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory);
                    string lnk2 = Path.Combine(commonDesktop, "Đá Cuội Mon.lnk");
                    if (File.Exists(lnk2)) File.Delete(lnk2);
                }
                catch { }

                // 3. Remove Start Menu Folders
                try
                {
                    string userSm = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), "Đá Cuội Mon");
                    if (Directory.Exists(userSm)) Directory.Delete(userSm, true);

                    string commonSm = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms), "Đá Cuội Mon");
                    if (Directory.Exists(commonSm)) Directory.Delete(commonSm, true);
                }
                catch { }

                // 4. Remove Registry entry
                try
                {
                    Registry.CurrentUser.DeleteSubKeyTree(@"Software\Microsoft\Windows\CurrentVersion\Uninstall\DaCuoiMon", false);
                }
                catch { }

                // 5. Schedule self-deletion of the entire installation directory (wipes data, mods, and Java)
                string installDir = AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\', '/');

                var psi = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = $"/c timeout /t 2 >nul & rmdir /s /q \"{installDir}\"",
                    WindowStyle = ProcessWindowStyle.Hidden,
                    CreateNoWindow = true,
                    UseShellExecute = true
                };
                Process.Start(psi);
            });

            MessageBox.Show("Đá Cuội Mon cùng toàn bộ 9 bản mod, dữ liệu game và Java 21 đã được dọn dẹp sạch sẽ hoàn toàn khỏi máy tính!", "Gỡ Cài Đặt Hoàn Tất", MessageBoxButton.OK, MessageBoxImage.Information);

            Application.Current.Shutdown();
        }
    }

    /// <summary>
    /// 100% Native Windows Shell COM shortcut creator with UTF-16 Unicode support
    /// (supports Vietnamese, Asian characters, special symbols without ANSI degradation)
    /// </summary>
    internal static class NativeShortcut
    {
        [ComImport]
        [Guid("00021401-0000-0000-C000-000000000046")]
        private class ShellLink { }

        [ComImport]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        [Guid("000214F9-0000-0000-C000-000000000046")]
        private interface IShellLinkW
        {
            void GetPath([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszFile, int cchMaxPath, out IntPtr pfd, uint fFlags);
            void GetIDList(out IntPtr ppidl);
            void SetIDList(IntPtr pidl);
            void GetDescription([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszName, int cchMaxName);
            void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string pszName);
            void GetWorkingDirectory([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszDir, int cchMaxPath);
            void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string pszDir);
            void GetArguments([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszArgs, int cchMaxPath);
            void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string pszArgs);
            void GetHotkey(out short pwHotkey);
            void SetHotkey(short wHotkey);
            void GetShowCmd(out int piShowCmd);
            void SetShowCmd(int iShowCmd);
            void GetIconLocation([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszIconPath, int cchIconPath, out int piIcon);
            void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string pszIconPath, int iIcon);
            void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string pszPathRel, uint dwReserved);
            void Resolve(IntPtr hwnd, uint fFlags);
            void SetPath([MarshalAs(UnmanagedType.LPWStr)] string pszFile);
        }

        [ComImport]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        [Guid("0000010b-0000-0000-C000-000000000046")]
        private interface IPersistFile
        {
            void GetClassID(out Guid pClassID);
            void IsDirty();
            void Load([MarshalAs(UnmanagedType.LPWStr)] string pszFileName, uint dwMode);
            void Save([MarshalAs(UnmanagedType.LPWStr)] string pszFileName, [MarshalAs(UnmanagedType.Bool)] bool fRemember);
            void SaveCompleted([MarshalAs(UnmanagedType.LPWStr)] string pszFileName);
            void GetCurFile([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder ppszFileName);
        }

        public static void Create(string shortcutPath, string targetPath, string workingDir, string description, string iconPath)
        {
            try
            {
                string? dir = Path.GetDirectoryName(shortcutPath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                var link = (IShellLinkW)new ShellLink();
                link.SetPath(targetPath);
                link.SetWorkingDirectory(workingDir);
                link.SetDescription(description);

                if (!string.IsNullOrEmpty(iconPath) && File.Exists(iconPath))
                {
                    link.SetIconLocation(iconPath, 0);
                }

                var file = (IPersistFile)link;
                file.Save(shortcutPath, false);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error creating shortcut: {ex.Message}");
            }
        }
    }
}