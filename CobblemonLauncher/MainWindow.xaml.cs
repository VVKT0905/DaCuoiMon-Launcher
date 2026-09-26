using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CobblemonLauncher.Models;
using CobblemonLauncher.Services;

namespace CobblemonLauncher
{
    public partial class MainWindow : Window
    {
        private const string ServerIpUrl = "https://raw.githubusercontent.com/VVKT0905/DaCuoiMon-Launcher/main/testip";
        private AppConfig _config;
        private string _serverAddress = "127.0.0.1:25565";
        private bool _isDebugMode = false;
        private RemoteConfig? _remoteConfig;

        public MainWindow()
        {
            string logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "app.log");
            File.AppendAllText(logPath, $"[{DateTime.Now}] MainWindow constructor start\n");
            try
            {
                InitializeComponent();
                File.AppendAllText(logPath, $"[{DateTime.Now}] InitializeComponent success\n");

                _config = AppConfig.Load();
                _serverAddress = _config.ServerAddress;
                LoadConfigToUi();

                TxtLauncherVersion.Text = $"v{LauncherUpdateService.GetCurrentVersion()}";
                UpdateCoinDisplay();

                CheckDebugMode();

                MinecraftLauncherService.OnLogOutput += LogMessage;
                File.AppendAllText(logPath, $"[{DateTime.Now}] MainWindow constructor finished\n");
            }
            catch (Exception ex)
            {
                File.AppendAllText(logPath, $"[{DateTime.Now}] MainWindow constructor EXCEPTION: {ex}\n");
                throw;
            }
        }

        private void CheckDebugMode()
        {
            string[] args = Environment.GetCommandLineArgs();
            foreach (var arg in args)
            {
                if (string.Equals(arg, "-debug", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(arg, "--debug", StringComparison.OrdinalIgnoreCase))
                {
                    _isDebugMode = true;
                    break;
                }
            }

            if (_isDebugMode)
            {
                TxtDebugIp.Visibility = Visibility.Visible;
                TxtDebugIp.Text = $"IP: {_serverAddress} (Đang đồng bộ...)";
            }
            else
            {
                TxtDebugIp.Visibility = Visibility.Collapsed;
            }
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                LauncherUpdateService.CleanupOldFiles();
                AssetService.EnsureAssets();
                LoadMascot();
                SkinService.RemoveBuiltinModels(_config.GetEffectiveGameDir());

                // Check trial skin expiration & daily login status
                SkinService.CheckAndExpireTrialSkins(_config, _config.GetEffectiveGameDir());
                var (canClaimDaily, nextDay, reward) = SkinService.GetDailyCheckInStatus(_config);
                UpdateCoinDisplay();
                if (canClaimDaily)
                {
                    LogMessage($"[Điểm Danh] Bạn chưa điểm danh hôm nay (Ngày {nextDay}: +{reward:N0} Đá Cuội). Bấm vào thanh 🪨 để nhận thưởng!");
                }
                else
                {
                    LogMessage($"[Điểm Danh] Bạn đã hoàn thành điểm danh hôm nay (Chuỗi: {_config.CheckInStreak}/7 ngày).");
                }

                // Fetch latest remote config (IP, launcher version, mods list, skins)
                await FetchRemoteConfigAndCheckUpdateAsync();
                UpdateCoinDisplay();
            }
            catch (Exception ex)
            {
                LogMessage($"[Startup Warning] {ex.Message}");
            }
        }

        private async Task FetchRemoteConfigAndCheckUpdateAsync()
        {
            string gameDir = _config.GetEffectiveGameDir();
            try
            {
                _remoteConfig = await RemoteConfigService.FetchConfigAsync(ServerIpUrl, gameDir);
                int skinCount = _remoteConfig?.Skins?.Count ?? 0;
                LogMessage($"[Cấu hình] Server IP: {_remoteConfig?.ServerIp}, Skins: {skinCount}");
                try
                {
                    string logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "app.log");
                    File.AppendAllText(logPath, $"[{DateTime.Now}] RemoteConfig: ServerIp={_remoteConfig?.ServerIp}, Skins={skinCount}\n");
                }
                catch { }

                if (!string.IsNullOrWhiteSpace(_remoteConfig?.ServerIp))
                {
                    _serverAddress = _remoteConfig.ServerIp.Trim();
                    _config.ServerAddress = _serverAddress;
                    _config.Save();

                    // Wipe multiplayer list and set only this server
                    ServerListService.SetSingleServer(gameDir, "Đá Cuội Mon", _serverAddress);

                    if (_isDebugMode)
                    {
                        Dispatcher.Invoke(() =>
                        {
                            TxtDebugIp.Text = $"Server: {_serverAddress}";
                        });
                    }
                }

                // Check auto-update for launcher (v2.0.0 Industry-Standard Update)
                string currentVersion = LauncherUpdateService.GetCurrentVersion();
                if (_remoteConfig != null &&
                    LauncherUpdateService.IsNewerVersion(_remoteConfig.LauncherVersion, currentVersion) &&
                    !string.IsNullOrWhiteSpace(_remoteConfig.LauncherDownloadUrl))
                {
                    LogMessage($"[Update] Phát hiện bản cập nhật mới: v{_remoteConfig.LauncherVersion} (hiện tại: v{currentVersion})");

                    // Transition UI to Modern HUD Gaming Update Screen
                    Dispatcher.Invoke(() =>
                    {
                        PanelPreLaunch.Visibility = Visibility.Collapsed;
                        PanelSkinShop.Visibility = Visibility.Collapsed;
                        PanelCobbleCenter.Visibility = Visibility.Collapsed;
                        PanelLoading.Visibility = Visibility.Collapsed;
                        PanelUpdateHUD.Visibility = Visibility.Visible;

                        TxtUpdateHeader.Text = $"Bản cập nhật v{_remoteConfig.LauncherVersion}";
                        TxtUpdateStatus.Text = "Đang kết nối máy chủ để cập nhật...";
                        TxtUpdateSpeed.Text = "-- MB/s";
                        ProgressBarUpdate.Value = 0;
                        TxtUpdatePercent.Text = "0%";

                        // Populate Changelog if present
                        if (_remoteConfig.LauncherChangelog != null && _remoteConfig.LauncherChangelog.Count > 0)
                        {
                            ContainerChangelog.Children.Clear();
                            var title = new TextBlock
                            {
                                Text = $"✨ ĐIỂM MỚI TRÊN PHIÊN BẢN {_remoteConfig.LauncherVersion}:",
                                FontSize = 11,
                                FontWeight = FontWeights.Bold,
                                Foreground = new SolidColorBrush(Color.FromRgb(0x38, 0xBD, 0xF8)),
                                Margin = new Thickness(0, 0, 0, 6)
                            };
                            ContainerChangelog.Children.Add(title);

                            foreach (var line in _remoteConfig.LauncherChangelog)
                            {
                                var item = new TextBlock
                                {
                                    Text = $"• {line}",
                                    FontSize = 11,
                                    Foreground = new SolidColorBrush(Color.FromRgb(0xCB, 0xD5, 0xE1)),
                                    TextWrapping = TextWrapping.Wrap,
                                    Margin = new Thickness(0, 0, 0, 4)
                                };
                                ContainerChangelog.Children.Add(item);
                            }
                        }
                    });

                    var updateProgress = new Progress<UpdateProgressInfo>(report =>
                    {
                        Dispatcher.Invoke(() =>
                        {
                            ProgressBarUpdate.Value = report.Percent;
                            TxtUpdatePercent.Text = $"{(int)report.Percent}%";
                            TxtUpdateStatus.Text = report.StatusText;

                            if (report.SpeedMBps > 0)
                            {
                                TxtUpdateSpeed.Text = $"{report.SpeedMBps:F1} MB/s";
                            }

                            if (!string.IsNullOrEmpty(report.ErrorMessage))
                            {
                                TxtUpdateStatus.Foreground = new SolidColorBrush(Color.FromRgb(0xF8, 0x71, 0x71));
                                TxtUpdateSubStatus.Text = report.ErrorMessage;
                            }
                        });
                    });

                    bool success = await LauncherUpdateService.DownloadAndApplyUpdateAsync(
                        _remoteConfig.LauncherDownloadUrl,
                        _remoteConfig.LauncherSha256,
                        updateProgress);

                    if (!success)
                    {
                        // Fallback gracefully to main panel after warning
                        await Task.Delay(3000);
                        Dispatcher.Invoke(() =>
                        {
                            PanelUpdateHUD.Visibility = Visibility.Collapsed;
                            PanelPreLaunch.Visibility = Visibility.Visible;
                        });
                    }
                    return;
                }
            }
            catch (Exception ex)
            {
                // Fallback to saved server address if network offline
                ServerListService.SetSingleServer(gameDir, "Đá Cuội Mon", _serverAddress);

                if (_isDebugMode)
                {
                    Dispatcher.Invoke(() =>
                    {
                        TxtDebugIp.Text = $"Server (Offline): {_serverAddress}";
                    });
                }
                LogMessage($"[Network] Không thể tải config mới nhất: {ex.Message}, sử dụng IP cấu hình: {_serverAddress}");
            }
        }

        private void LoadMascot()
        {
            string mascotPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "mascot.png");
            if (File.Exists(mascotPath))
            {
                try
                {
                    var bmp = new BitmapImage();
                    bmp.BeginInit();
                    bmp.UriSource = new Uri(mascotPath, UriKind.Absolute);
                    bmp.CacheOption = BitmapCacheOption.OnLoad;
                    bmp.EndInit();
                    ImgMascot.Source = bmp;
                }
                catch { }
            }
        }

        private void LoadConfigToUi()
        {
            TxtPlayerName.Text = string.IsNullOrWhiteSpace(_config.PlayerName) ? "PixelMaster7" : _config.PlayerName;
        }

        private void SaveUiToConfig()
        {
            _config.PlayerName = TxtPlayerName.Text.Trim();
            _config.ServerAddress = _serverAddress;
            _config.Save();
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
            SaveUiToConfig();
            Close();
        }

        private async void BtnPlay_Click(object sender, RoutedEventArgs e)
        {
            SaveUiToConfig();

            // Clear console logs upon clicking PLAY
            TxtLogs.Text = string.Empty;

            // Transition UI: Hide name & play button, show loading bar & logs
            PanelPreLaunch.Visibility = Visibility.Collapsed;
            PanelLoading.Visibility = Visibility.Visible;

            string gameDir = _config.GetEffectiveGameDir();
            string serverAddress = _serverAddress;
            int ramGb = _config.AllocatedRamGb;
            bool directConnect = _config.AutoDirectConnect;

            var progress = new Progress<(double percent, string status)>(report =>
            {
                Dispatcher.Invoke(() =>
                {
                    ProgressBarLoading.Value = report.percent;
                    TxtProgressPercent.Text = $"{(int)report.percent}%";
                    TxtStatus.Text = report.status;
                });
            });

            try
            {
                TxtStatus.Text = "Đang chuẩn bị Java 21 Portable...";
                string javaPath = await Task.Run(() => JavaService.EnsureJava21Async(gameDir, progress));

                TxtStatus.Text = "Đang kiểm tra modpack & mod bổ sung...";
                await Task.Run(() => ModpackService.EnsureModpackAndModsAsync(gameDir, serverAddress, _remoteConfig?.AdditionalMods, progress));

                // Always wipe old servers and ensure ONLY the current server address is configured
                ServerListService.SetSingleServer(gameDir, "Đá Cuội Mon", serverAddress);

                // Player Session (Offline directly with entered player name)
                string playerName = string.IsNullOrWhiteSpace(_config.PlayerName) ? "PixelMaster7" : _config.PlayerName;
                PlayerSession session = AuthService.CreateOfflineSession(playerName);

                // Enforce owned models only (anti-tamper / anti-cheat)
                SkinService.EnforceOwnedModels(_config, gameDir, _remoteConfig?.Skins);

                TxtStatus.Text = "Đang khởi chạy Minecraft Cobblemon...";
                var proc = await Task.Run(() => MinecraftLauncherService.LaunchAsync(
                    javaPath,
                    gameDir,
                    ramGb,
                    session,
                    serverAddress,
                    directConnect,
                    progress
                ));

                ProgressBarLoading.Value = 100;
                TxtProgressPercent.Text = "100%";
                TxtStatus.Text = "Loading...";

                if (proc != null)
                {
                    // Monitor until Minecraft window is visible on screen, then hide launcher and award playtime
                    await MonitorGameAndAwardPlaytimeAsync(proc);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Đã xảy ra sự cố khi khởi chạy Cobblemon:\n{ex.Message}", "Lỗi Khởi Chạy", MessageBoxButton.OK, MessageBoxImage.Error);
                TxtStatus.Text = "Khởi chạy thất bại. Kiểm tra log bên dưới.";
                LogMessage($"[LỖI NGHIÊM TRỌNG] {ex}");

                // Restore pre-launch panel on error so user can retry
                PanelPreLaunch.Visibility = Visibility.Visible;
                PanelLoading.Visibility = Visibility.Collapsed;
            }
        }

        private async Task MonitorGameAndAwardPlaytimeAsync(Process proc)
        {
            await Task.Run(async () =>
            {
                bool gameWindowAppeared = false;
                int elapsedSeconds = 0;

                while (!proc.HasExited && elapsedSeconds < 180) // Max 3 minutes wait
                {
                    await Task.Delay(1000);
                    elapsedSeconds++;

                    try
                    {
                        proc.Refresh();

                        // Check if Minecraft window handle exists and title has appeared
                        if (proc.MainWindowHandle != IntPtr.Zero)
                        {
                            string title = proc.MainWindowTitle;
                            if (!string.IsNullOrWhiteSpace(title) &&
                                (title.Contains("Minecraft", StringComparison.OrdinalIgnoreCase) ||
                                 title.Contains("Cobblemon", StringComparison.OrdinalIgnoreCase) ||
                                 title.Length > 2))
                            {
                                gameWindowAppeared = true;
                                break;
                            }
                        }
                    }
                    catch { }
                }

                if (gameWindowAppeared)
                {
                    Dispatcher.Invoke(() =>
                    {
                        TxtStatus.Text = "Minecraft đang chạy! Launcher đã ẩn để tính thời gian nhận Đá Cuội...";
                        Hide();
                    });

                    int playSeconds = 0;
                    while (!proc.HasExited)
                    {
                        await Task.Delay(1000);
                        playSeconds++;

                        // Every 5 minutes (300 seconds) -> +100 Đá Cuội
                        if (playSeconds % 300 == 0)
                        {
                            SkinService.AwardPlayTime(_config, 300, 100);
                            Dispatcher.Invoke(() =>
                            {
                                UpdateCoinDisplay();
                                LogMessage($"[Phần Thưởng Chơi Game] +100 Đá Cuội (5 phút chơi)! Số dư: {_config.CobbleCoins:N0}");
                            });
                        }

                        // Every 1 hour (3600 seconds) -> Thưởng thêm +200 Đá Cuội!
                        if (playSeconds % 3600 == 0)
                        {
                            SkinService.AwardPlayTime(_config, 0, 200);
                            Dispatcher.Invoke(() =>
                            {
                                UpdateCoinDisplay();
                                LogMessage($"[Thưởng 1 Giờ Chơi] Thưởng thêm +200 Đá Cuội mốc 1 giờ! Số dư: {_config.CobbleCoins:N0}");
                            });
                        }
                    }

                    // Minecraft exited, restore Launcher
                    Dispatcher.Invoke(() =>
                    {
                        Show();
                        WindowState = WindowState.Normal;
                        Activate();
                        PanelPreLaunch.Visibility = Visibility.Visible;
                        PanelLoading.Visibility = Visibility.Collapsed;
                        UpdateCoinDisplay();
                        LogMessage($"[Kết Thúc Game] Phiên chơi: {playSeconds / 60} phút {playSeconds % 60} giây. Tổng số dư: {_config.CobbleCoins:N0} Đá Cuội.");
                    });
                }
                else
                {
                    // Minecraft exited or crashed before its main window appeared
                    Dispatcher.Invoke(() =>
                    {
                        Show();
                        WindowState = WindowState.Normal;
                        Activate();
                        PanelPreLaunch.Visibility = Visibility.Visible;
                        PanelLoading.Visibility = Visibility.Collapsed;

                        int exitCode = -1;
                        try { exitCode = proc.ExitCode; } catch { }

                        string crashDetails = "";
                        try
                        {
                            string crashDir = Path.Combine(_config.GetEffectiveGameDir(), "crash-reports");
                            if (Directory.Exists(crashDir))
                            {
                                var latestCrash = new DirectoryInfo(crashDir)
                                    .GetFiles("crash-*.txt")
                                    .OrderByDescending(f => f.LastWriteTime)
                                    .FirstOrDefault();
                                if (latestCrash != null && (DateTime.Now - latestCrash.LastWriteTime).TotalMinutes < 3)
                                {
                                    crashDetails = $" (Xem báo cáo: {latestCrash.Name})";
                                }
                            }
                        }
                        catch { }

                        TxtStatus.Text = $"Game đã đóng đột ngột (Mã lỗi: {exitCode}).{crashDetails}";
                        LogMessage($"[CẢNH BÁO] Minecraft đã thoát trước khi hiển thị màn hình (Mã thoát: {exitCode}).{crashDetails}");
                    });
                }
            });
        }

        private string _previousPanelBeforeCobbleCenter = "main";
        private int _currentSessionPlaySeconds = 0;

        private void UpdateCoinDisplay()
        {
            Dispatcher.Invoke(() =>
            {
                string coinStr = _config.CobbleCoins.ToString("N0");
                TxtShopCoins.Text = coinStr;
                if (TxtMainCoins != null)
                {
                    TxtMainCoins.Text = coinStr;
                }
                if (TxtCenterCoins != null)
                {
                    TxtCenterCoins.Text = coinStr;
                }
            });
        }

        private void CoinBar_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                e.Handled = true;
                bool fromShop = PanelSkinShop.Visibility == Visibility.Visible;
                OpenCobbleCenter(fromShop);
            }
        }

        private void CoinBar_Click(object sender, RoutedEventArgs e)
        {
            bool fromShop = PanelSkinShop.Visibility == Visibility.Visible;
            OpenCobbleCenter(fromShop);
        }

        private void OpenCobbleCenter(bool fromShop)
        {
            _previousPanelBeforeCobbleCenter = fromShop ? "shop" : "main";
            double targetWidth = 780;
            double targetHeight = 600;

            if (Width != targetWidth)
            {
                Left = Left - (targetWidth - Width) / 2;
                Top = Top - (targetHeight - Height) / 2;
                Width = targetWidth;
                Height = targetHeight;
            }

            PanelPreLaunch.Visibility = Visibility.Collapsed;
            PanelSkinShop.Visibility = Visibility.Collapsed;
            PanelCobbleCenter.Visibility = Visibility.Visible;
            BtnLauncherClose.Visibility = Visibility.Collapsed;

            SkinService.CheckAndExpireTrialSkins(_config, _config.GetEffectiveGameDir());
            UpdateCoinDisplay();
            RefreshStreakUI();
            RefreshPlaytimeUI();

            TxtGiftcodeInput.Text = "";
            BorderGiftcodeResult.Visibility = Visibility.Collapsed;
        }

        private void BtnCloseCobbleCenter_Click(object sender, RoutedEventArgs e)
        {
            if (_previousPanelBeforeCobbleCenter == "shop")
            {
                PanelCobbleCenter.Visibility = Visibility.Collapsed;
                OpenSkinShop();
            }
            else
            {
                double targetWidth = 510;
                double targetHeight = 470;

                if (Width != targetWidth)
                {
                    Left = Left + (Width - targetWidth) / 2;
                    Top = Top + (Height - targetHeight) / 2;
                    Width = targetWidth;
                    Height = targetHeight;
                }

                PanelCobbleCenter.Visibility = Visibility.Collapsed;
                PanelPreLaunch.Visibility = Visibility.Visible;
                BtnLauncherClose.Visibility = Visibility.Visible;
                UpdateCoinDisplay();
            }
        }

        private void BtnClaimDaily_Click(object sender, RoutedEventArgs e)
        {
            var (awarded, amount, streakDay) = SkinService.ClaimDailyCheckIn(_config);
            if (awarded)
            {
                UpdateCoinDisplay();
                RefreshStreakUI();
                LogMessage($"[Điểm Danh] Điểm danh ngày {streakDay} thành công! +{amount:N0} Đá Cuội. Số dư: {_config.CobbleCoins:N0}");
                MessageBox.Show($"Chúc mừng! Bạn đã điểm danh ngày thứ {streakDay} thành công và nhận được +{amount:N0} Đá Cuội! 🪨", "Điểm Danh Thành Công", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void RefreshStreakUI()
        {
            GridStreakDays.Children.Clear();
            var (canClaim, nextStreakDay, nextReward) = SkinService.GetDailyCheckInStatus(_config);
            TxtStreakStatus.Text = $"Chuỗi điểm danh: {_config.CheckInStreak}/7 ngày";

            for (int day = 1; day <= 7; day++)
            {
                int reward = day == 7 ? 3000 : 1000;
                bool isClaimed = day <= _config.CheckInStreak && (!canClaim || day < nextStreakDay);
                bool isTodayTarget = canClaim && day == nextStreakDay;

                var dayBox = new System.Windows.Controls.Border
                {
                    Margin = new Thickness(3),
                    CornerRadius = new CornerRadius(10),
                    Padding = new Thickness(4, 8, 4, 8),
                    Background = isClaimed
                        ? new SolidColorBrush(Color.FromArgb(0x35, 0x00, 0xFF, 0x9D))
                        : (isTodayTarget
                            ? new SolidColorBrush(Color.FromArgb(0x40, 0x38, 0xBD, 0xF8))
                            : new SolidColorBrush(Color.FromArgb(0x18, 0xFF, 0xFF, 0xFF))),
                    BorderThickness = new Thickness(isTodayTarget ? 1.5 : 1),
                    BorderBrush = isClaimed
                        ? new SolidColorBrush(Color.FromRgb(0x00, 0xFF, 0x9D))
                        : (isTodayTarget
                            ? new SolidColorBrush(Color.FromRgb(0x38, 0xBD, 0xF8))
                            : new SolidColorBrush(Color.FromArgb(0x25, 0xFF, 0xFF, 0xFF)))
                };

                var stack = new System.Windows.Controls.StackPanel
                {
                    HorizontalAlignment = HorizontalAlignment.Center
                };

                stack.Children.Add(new TextBlock
                {
                    Text = $"NGÀY {day}",
                    FontSize = 10,
                    FontWeight = FontWeights.Bold,
                    Foreground = isClaimed
                        ? new SolidColorBrush(Color.FromRgb(0x00, 0xFF, 0x9D))
                        : (isTodayTarget ? new SolidColorBrush(Color.FromRgb(0x38, 0xBD, 0xF8)) : new SolidColorBrush(Color.FromRgb(0x94, 0xA3, 0xB8))),
                    HorizontalAlignment = HorizontalAlignment.Center
                });

                stack.Children.Add(new TextBlock
                {
                    Text = isClaimed ? "✔" : (day == 7 ? "⭐" : "🎁"),
                    FontFamily = new FontFamily("Segoe UI Emoji, Segoe UI Symbol"),
                    FontSize = 16,
                    Margin = new Thickness(0, 3, 0, 3),
                    HorizontalAlignment = HorizontalAlignment.Center
                });

                stack.Children.Add(new TextBlock
                {
                    Text = $"+{reward:N0}",
                    FontSize = 10,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = Brushes.White,
                    HorizontalAlignment = HorizontalAlignment.Center
                });

                dayBox.Child = stack;
                GridStreakDays.Children.Add(dayBox);
            }

            if (canClaim)
            {
                BtnClaimDaily.IsEnabled = true;
                BtnClaimDaily.Content = $"🎁 ĐIỂM DANH NGÀY {nextStreakDay} (+{nextReward:N0} 🪨)";
                BtnClaimDaily.Template = CreateCustomButtonTemplate(
                    new SolidColorBrush(Color.FromRgb(0x00, 0xFF, 0x9D)),
                    new SolidColorBrush(Color.FromRgb(0x00, 0xFF, 0x9D)),
                    new SolidColorBrush(Color.FromRgb(0x05, 0x33, 0x1E)), 10
                );
            }
            else
            {
                BtnClaimDaily.IsEnabled = false;
                BtnClaimDaily.Content = "✔ BẠN ĐÃ ĐIỂM DANH HÔM NAY (Hẹn gặp lại ngày mai!)";
                BtnClaimDaily.Template = CreateCustomButtonTemplate(
                    new SolidColorBrush(Color.FromArgb(0x20, 0x00, 0xFF, 0x9D)),
                    new SolidColorBrush(Color.FromArgb(0x50, 0x00, 0xFF, 0x9D)),
                    new SolidColorBrush(Color.FromRgb(0x00, 0xFF, 0x9D)), 10
                );
            }
        }

        private void RefreshPlaytimeUI()
        {
            int totalSec = _config.TotalPlayTimeSeconds + _currentSessionPlaySeconds;
            TxtTotalPlayTime.Text = $"Tổng đã chơi: {totalSec / 3600}h {(totalSec % 3600) / 60}m";

            int secIn5Min = totalSec % 300;
            ProgressBar5Min.Value = secIn5Min;
            TxtProgress5Min.Text = $"{secIn5Min / 60:D2}:{secIn5Min % 60:D2} / 05:00";

            int secInHour = totalSec % 3600;
            ProgressBar1Hour.Value = secInHour;
            TxtProgress1Hour.Text = $"{secInHour / 60:D2}:{secInHour % 60:D2} / 60:00";
        }

        private async void BtnRedeemGiftcode_Click(object sender, RoutedEventArgs e)
        {
            string code = TxtGiftcodeInput.Text.Trim();
            if (string.IsNullOrWhiteSpace(code))
            {
                BorderGiftcodeResult.Background = new SolidColorBrush(Color.FromArgb(0x25, 0xEF, 0x44, 0x44));
                BorderGiftcodeResult.BorderBrush = new SolidColorBrush(Color.FromRgb(0xEF, 0x44, 0x44));
                BorderGiftcodeResult.BorderThickness = new Thickness(1);
                TxtGiftcodeResult.Foreground = new SolidColorBrush(Color.FromRgb(0xFC, 0xA5, 0xA5));
                TxtGiftcodeResult.Text = "Vui lòng nhập mã Giftcode!";
                BorderGiftcodeResult.Visibility = Visibility.Visible;
                return;
            }

            try
            {
                BtnRedeemGiftcode.IsEnabled = false;
                BtnRedeemGiftcode.Content = "Đang đổi...";

                var result = await GiftcodeService.RedeemGiftcodeAsync(_config, code, _config.GetEffectiveGameDir());
                UpdateCoinDisplay();
                TxtGiftcodeInput.Text = "";

                BorderGiftcodeResult.Background = new SolidColorBrush(Color.FromArgb(0x25, 0x00, 0xFF, 0x9D));
                BorderGiftcodeResult.BorderBrush = new SolidColorBrush(Color.FromRgb(0x00, 0xFF, 0x9D));
                BorderGiftcodeResult.BorderThickness = new Thickness(1);
                TxtGiftcodeResult.Foreground = new SolidColorBrush(Color.FromRgb(0x00, 0xFF, 0x9D));
                TxtGiftcodeResult.Text = $"✔ ĐỔI QUÀ THÀNH CÔNG!\n{result.BuildSummaryMessage()}";
                BorderGiftcodeResult.Visibility = Visibility.Visible;

                LogMessage($"[Giftcode] Đổi thành công mã quà tặng: {result.BuildSummaryMessage().Replace("\n", ", ")}");
                MessageBox.Show($"Chúc mừng! Bạn đã đổi mã Giftcode thành công:\n\n{result.BuildSummaryMessage()}", "Đổi Quà Thành Công", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                BorderGiftcodeResult.Background = new SolidColorBrush(Color.FromArgb(0x25, 0xEF, 0x44, 0x44));
                BorderGiftcodeResult.BorderBrush = new SolidColorBrush(Color.FromRgb(0xEF, 0x44, 0x44));
                BorderGiftcodeResult.BorderThickness = new Thickness(1);
                TxtGiftcodeResult.Foreground = new SolidColorBrush(Color.FromRgb(0xFC, 0xA5, 0xA5));
                TxtGiftcodeResult.Text = $"❌ Lỗi: {ex.Message}";
                BorderGiftcodeResult.Visibility = Visibility.Visible;
            }
            finally
            {
                BtnRedeemGiftcode.IsEnabled = true;
                BtnRedeemGiftcode.Content = "ĐỔI MÃ";
            }
        }

        private string _currentSkinFilter = "all";
        private string _searchQuery = "";

        private void CardGrid_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (CardClipGeometry != null)
            {
                CardClipGeometry.Rect = new Rect(0, 0, e.NewSize.Width, e.NewSize.Height);
            }
        }

        private void TxtSkinSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            _searchQuery = TxtSkinSearch.Text.Trim();
            RenderSkinCards();
        }

        private void BtnFilter_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string filter)
            {
                _currentSkinFilter = filter;
                UpdateFilterButtonsStyle();
                RenderSkinCards();
            }
        }

        private void UpdateFilterButtonsStyle()
        {
            SetFilterButtonActive(BtnFilterAll, _currentSkinFilter == "all");
            SetFilterButtonActive(BtnFilterOwned, _currentSkinFilter == "owned");
            SetFilterButtonActive(BtnFilterUnowned, _currentSkinFilter == "unowned");
        }

        private void SetFilterButtonActive(Button? btn, bool isActive)
        {
            if (btn == null) return;
            if (isActive)
            {
                btn.Background = new SolidColorBrush(Color.FromArgb(0x35, 0x00, 0xFF, 0x9D));
                btn.BorderBrush = new SolidColorBrush(Color.FromRgb(0x00, 0xFF, 0x9D));
                btn.Foreground = new SolidColorBrush(Color.FromRgb(0x00, 0xFF, 0x9D));
            }
            else
            {
                btn.Background = new SolidColorBrush(Color.FromArgb(0x1F, 0xFF, 0xFF, 0xFF));
                btn.BorderBrush = new SolidColorBrush(Color.FromArgb(0x25, 0xFF, 0xFF, 0xFF));
                btn.Foreground = Brushes.White;
            }
        }

        private void BtnOpenSkinShop_Click(object sender, RoutedEventArgs e)
        {
            OpenSkinShop();
        }

        private void BtnCloseSkinShop_Click(object sender, RoutedEventArgs e)
        {
            double targetWidth = 510;
            double targetHeight = 470;

            if (Width != targetWidth)
            {
                Left = Left + (Width - targetWidth) / 2;
                Top = Top + (Height - targetHeight) / 2;
                Width = targetWidth;
                Height = targetHeight;
            }

            PanelSkinShop.Visibility = Visibility.Collapsed;
            PanelPreLaunch.Visibility = Visibility.Visible;
            BtnLauncherClose.Visibility = Visibility.Visible;
            UpdateCoinDisplay();
        }

        private void OpenSkinShop()
        {
            double targetWidth = 960;
            double targetHeight = 670;

            if (Width != targetWidth)
            {
                Left = Left - (targetWidth - Width) / 2;
                Top = Top - (targetHeight - Height) / 2;
                Width = targetWidth;
                Height = targetHeight;
            }

            PanelPreLaunch.Visibility = Visibility.Collapsed;
            PanelSkinShop.Visibility = Visibility.Visible;
            BtnLauncherClose.Visibility = Visibility.Collapsed;
            UpdateCoinDisplay();
            _currentSkinFilter = "all";
            _searchQuery = "";
            TxtSkinSearch.Text = "";
            UpdateFilterButtonsStyle();
            RenderSkinCards();
        }

        private static ControlTemplate CreateCustomButtonTemplate(Brush bg, Brush border, Brush fg, double cornerRadius = 10)
        {
            var template = new ControlTemplate(typeof(System.Windows.Controls.Button));
            var borderFactory = new FrameworkElementFactory(typeof(System.Windows.Controls.Border));
            borderFactory.SetValue(System.Windows.Controls.Border.BackgroundProperty, bg);
            borderFactory.SetValue(System.Windows.Controls.Border.BorderBrushProperty, border);
            borderFactory.SetValue(System.Windows.Controls.Border.BorderThicknessProperty, new Thickness(1));
            borderFactory.SetValue(System.Windows.Controls.Border.CornerRadiusProperty, new CornerRadius(cornerRadius));
            borderFactory.SetValue(System.Windows.Controls.Border.PaddingProperty, new Thickness(10, 3, 10, 3));

            var contentFactory = new FrameworkElementFactory(typeof(System.Windows.Controls.ContentPresenter));
            contentFactory.SetValue(System.Windows.Controls.ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            contentFactory.SetValue(System.Windows.Controls.ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
            borderFactory.AppendChild(contentFactory);

            template.VisualTree = borderFactory;
            return template;
        }

        private string? GetSkinPreviewImage(SkinItem skin)
        {
            string gameDir = _config.GetEffectiveGameDir();
            string baseName = Path.GetFileNameWithoutExtension(skin.FileName);

            var candidateNames = new List<string> { baseName, skin.Id };

            var searchDirs = new List<string>
            {
                Path.Combine(gameDir, "models_pool"),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "models_pool"),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "SkinPreviews"),
                Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "CobblemonLauncher", "Assets", "SkinPreviews")),
                Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "Assets", "SkinPreviews")),
                Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "YSM_Models_Pool")),
                Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "YSM_Models_Pool")),
                Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "YSM_Models_Pool"))
            };

            foreach (var dir in searchDirs)
            {
                if (!Directory.Exists(dir)) continue;
                foreach (var name in candidateNames)
                {
                    foreach (var ext in new[] { ".png", ".jpg", ".jpeg", ".webp" })
                    {
                        string p = Path.Combine(dir, name + ext);
                        if (File.Exists(p)) return p;
                    }
                }
            }

            string? ysmPath = SkinService.FindYsmFilePath(skin, gameDir);
            if (!string.IsNullOrWhiteSpace(ysmPath))
            {
                string pngSidecar = Path.ChangeExtension(ysmPath, ".png");
                if (File.Exists(pngSidecar)) return pngSidecar;
            }

            return null;
        }

        private void RenderSkinCards()
        {
            ContainerSkinCards.Children.Clear();
            var allSkins = SkinService.GetEffectiveSkins(_remoteConfig?.Skins, _config.GetEffectiveGameDir());

            BtnFilterAll.Content = $"Tất cả ({allSkins.Count})";
            int ownedCount = allSkins.Count(s => _config.OwnedSkinIds.Contains(s.Id));
            BtnFilterOwned.Content = $"Đã sở hữu ({ownedCount})";
            BtnFilterUnowned.Content = $"Chưa sở hữu ({allSkins.Count - ownedCount})";

            var filtered = allSkins.Where(s =>
            {
                if (!string.IsNullOrWhiteSpace(_searchQuery) &&
                    !s.Name.Contains(_searchQuery, StringComparison.OrdinalIgnoreCase) &&
                    !s.Id.Contains(_searchQuery, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                bool isOwned = _config.OwnedSkinIds.Contains(s.Id);
                if (_currentSkinFilter == "owned" && !isOwned) return false;
                if (_currentSkinFilter == "unowned" && isOwned) return false;
                return true;
            }).ToList();

            foreach (var skin in filtered)
            {
                bool isOwned = _config.OwnedSkinIds.Contains(skin.Id);

                // Outer Shopee Product Card
                var cardBorder = new System.Windows.Controls.Border
                {
                    Width = 202,
                    Height = 242,
                    Margin = new Thickness(4),
                    CornerRadius = new CornerRadius(16),
                    Background = new SolidColorBrush(Color.FromArgb(0x28, 0x11, 0x16, 0x22)),
                    BorderThickness = new Thickness(1),
                    BorderBrush = new SolidColorBrush(isOwned
                        ? Color.FromArgb(0x60, 0x00, 0xFF, 0x9D)
                        : Color.FromArgb(0x20, 0xFF, 0xFF, 0xFF))
                };

                var cardGrid = new System.Windows.Controls.Grid();
                cardGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(130) }); // Image Box
                cardGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(64) });  // Details & Price
                cardGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(34) });  // Action Button

                // 1. TOP: 3D Preview Image Container
                var imageBox = new System.Windows.Controls.Border
                {
                    CornerRadius = new CornerRadius(13, 13, 0, 0),
                    ClipToBounds = true,
                    Background = new LinearGradientBrush(
                        Color.FromArgb(0x25, 0x1E, 0x29, 0x3B),
                        Color.FromArgb(0x0F, 0x0F, 0x17, 0x2A),
                        new Point(0, 0), new Point(0, 1)
                    )
                };

                var imageGrid = new System.Windows.Controls.Grid();

                var imgCtrl = new System.Windows.Controls.Image
                {
                    Stretch = Stretch.UniformToFill,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    Visibility = Visibility.Collapsed
                };

                var placeholderStack = new System.Windows.Controls.StackPanel
                {
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };
                placeholderStack.Children.Add(new TextBlock
                {
                    Text = "👘",
                    FontSize = 38,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Opacity = 0.85
                });
                placeholderStack.Children.Add(new TextBlock
                {
                    Text = "3D MODEL",
                    FontSize = 9,
                    FontWeight = FontWeights.Bold,
                    Foreground = new SolidColorBrush(Color.FromArgb(0x80, 0x00, 0xFF, 0x9D)),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 2, 0, 0)
                });

                imageGrid.Children.Add(placeholderStack);
                imageGrid.Children.Add(imgCtrl);

                string? previewFile = GetSkinPreviewImage(skin);
                if (previewFile != null)
                {
                    try
                    {
                        var bmp = new BitmapImage();
                        bmp.BeginInit();
                        bmp.UriSource = new Uri(previewFile, UriKind.Absolute);
                        bmp.DecodePixelWidth = 260;
                        bmp.CacheOption = BitmapCacheOption.OnLoad;
                        bmp.EndInit();

                        imgCtrl.Source = bmp;
                        imgCtrl.Visibility = Visibility.Visible;
                        placeholderStack.Visibility = Visibility.Collapsed;
                    }
                    catch
                    {
                        previewFile = null;
                    }
                }

                // Top Badge (Shopee Style)
                if (isOwned)
                {
                    DateTime expiryUtc = default;
                    bool isTrial = _config.TrialSkins != null && _config.TrialSkins.TryGetValue(skin.Id, out expiryUtc);
                    string badgeMsg = "✔ ĐÃ SỞ HỮU";
                    var badgeBg = new SolidColorBrush(Color.FromArgb(0xD5, 0x05, 0x33, 0x1E));
                    var badgeBorderBrush = new SolidColorBrush(Color.FromRgb(0x00, 0xFF, 0x9D));
                    var badgeFg = new SolidColorBrush(Color.FromRgb(0x00, 0xFF, 0x9D));

                    if (isTrial)
                    {
                        var remaining = expiryUtc - DateTime.UtcNow;
                        string remStr = remaining.TotalHours >= 24
                            ? $"{Math.Ceiling(remaining.TotalDays)}d"
                            : $"{Math.Max(1, Math.Ceiling(remaining.TotalHours))}h";
                        badgeMsg = $"⏳ DÙNG THỬ ({remStr})";
                        badgeBg = new SolidColorBrush(Color.FromArgb(0xD5, 0x4A, 0x1E, 0x05));
                        badgeBorderBrush = new SolidColorBrush(Color.FromRgb(0xFB, 0x92, 0x3C));
                        badgeFg = new SolidColorBrush(Color.FromRgb(0xFB, 0x92, 0x3C));
                    }

                    var badgeBorder = new System.Windows.Controls.Border
                    {
                        HorizontalAlignment = HorizontalAlignment.Left,
                        VerticalAlignment = VerticalAlignment.Top,
                        Margin = new Thickness(8, 8, 0, 0),
                        CornerRadius = new CornerRadius(8),
                        Padding = new Thickness(8, 2, 8, 2),
                        Background = badgeBg,
                        BorderThickness = new Thickness(1),
                        BorderBrush = badgeBorderBrush
                    };
                    var badgeText = new TextBlock
                    {
                        Text = badgeMsg,
                        FontSize = 10,
                        FontWeight = FontWeights.Bold,
                        Foreground = badgeFg
                    };
                    badgeBorder.Child = badgeText;
                    imageGrid.Children.Add(badgeBorder);
                }

                imageBox.Child = imageGrid;
                System.Windows.Controls.Grid.SetRow(imageBox, 0);
                cardGrid.Children.Add(imageBox);

                // 2. MIDDLE: Skin Name & Price
                var detailsStack = new System.Windows.Controls.StackPanel
                {
                    Margin = new Thickness(10, 5, 10, 4),
                    VerticalAlignment = VerticalAlignment.Center
                };

                var titleText = new TextBlock
                {
                    Text = skin.Name,
                    FontSize = 12,
                    FontWeight = FontWeights.Bold,
                    Foreground = Brushes.White,
                    TextTrimming = TextTrimming.CharacterEllipsis
                };
                detailsStack.Children.Add(titleText);

                var pricePanel = new System.Windows.Controls.StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Margin = new Thickness(0, 4, 0, 0)
                };

                if (skin.Id == "default" || isOwned)
                {
                    bool isTrial = _config.TrialSkins != null && _config.TrialSkins.ContainsKey(skin.Id);
                    string statusText = "Đã sở hữu";
                    if (isTrial && _config.TrialSkins != null && _config.TrialSkins.TryGetValue(skin.Id, out var expiryUtc))
                    {
                        var remaining = expiryUtc - DateTime.UtcNow;
                        if (remaining.TotalDays >= 1)
                        {
                            statusText = $"Dùng thử (còn {(int)remaining.TotalDays}d {remaining.Hours}h)";
                        }
                        else if (remaining.TotalHours >= 1)
                        {
                            statusText = $"Dùng thử (còn {(int)remaining.TotalHours}h {remaining.Minutes}m)";
                        }
                        else if (remaining.TotalMinutes > 0)
                        {
                            statusText = $"Dùng thử (còn {(int)remaining.TotalMinutes}m)";
                        }
                        else
                        {
                            statusText = "Dùng thử (hết hạn)";
                        }
                    }

                    pricePanel.Children.Add(new TextBlock
                    {
                        Text = statusText,
                        FontSize = 11,
                        FontWeight = FontWeights.SemiBold,
                        Foreground = new SolidColorBrush(isTrial ? Color.FromRgb(0xFB, 0x92, 0x3C) : Color.FromRgb(0x00, 0xFF, 0x9D))
                    });
                }
                else
                {
                    pricePanel.Children.Add(new TextBlock
                    {
                        Text = "🪨",
                        FontFamily = new FontFamily("Segoe UI Emoji, Segoe UI Symbol"),
                        FontSize = 12,
                        VerticalAlignment = VerticalAlignment.Center,
                        Margin = new Thickness(0, 0, 4, 0)
                    });
                    pricePanel.Children.Add(new TextBlock
                    {
                        Text = $"{skin.Price:N0} Đá Cuội",
                        FontSize = 11,
                        FontWeight = FontWeights.Bold,
                        Foreground = new SolidColorBrush(Color.FromRgb(0x00, 0xFF, 0x9D)),
                        VerticalAlignment = VerticalAlignment.Center
                    });
                }
                detailsStack.Children.Add(pricePanel);

                System.Windows.Controls.Grid.SetRow(detailsStack, 1);
                cardGrid.Children.Add(detailsStack);

                // 3. BOTTOM: Action Button
                var btnAction = new System.Windows.Controls.Button
                {
                    Height = 28,
                    Margin = new Thickness(10, 0, 10, 6),
                    FontWeight = FontWeights.Bold,
                    FontSize = 11,
                    FontFamily = new FontFamily("Segoe UI, Segoe UI Emoji, Segoe UI Symbol")
                };

                if (isOwned)
                {
                    bool isTrial = _config.TrialSkins != null && _config.TrialSkins.ContainsKey(skin.Id);
                    btnAction.Content = isTrial ? "⏳ ĐANG DÙNG THỬ" : "✔ ĐÃ SỞ HỮU";
                    btnAction.Foreground = isTrial ? new SolidColorBrush(Color.FromRgb(0xFB, 0x92, 0x3C)) : new SolidColorBrush(Color.FromRgb(0x00, 0xFF, 0x9D));
                    btnAction.Template = CreateCustomButtonTemplate(
                        new SolidColorBrush(isTrial ? Color.FromArgb(0x20, 0xFB, 0x92, 0x3C) : Color.FromArgb(0x20, 0x00, 0xFF, 0x9D)),
                        new SolidColorBrush(isTrial ? Color.FromArgb(0x60, 0xFB, 0x92, 0x3C) : Color.FromArgb(0x60, 0x00, 0xFF, 0x9D)),
                        btnAction.Foreground, 8
                    );
                    btnAction.IsEnabled = false;
                    btnAction.Cursor = Cursors.Arrow;
                }
                else
                {
                    bool canAfford = _config.CobbleCoins >= skin.Price;
                    btnAction.Content = canAfford ? $"ĐỔI ({skin.Price:N0} 🪨)" : $"CẦN {skin.Price:N0} 🪨";

                    if (canAfford)
                    {
                        btnAction.Foreground = new SolidColorBrush(Color.FromRgb(0x05, 0x33, 0x1E));
                        btnAction.Template = CreateCustomButtonTemplate(
                            new SolidColorBrush(Color.FromRgb(0x00, 0xFF, 0x9D)),
                            new SolidColorBrush(Color.FromRgb(0x00, 0xFF, 0x9D)),
                            btnAction.Foreground, 8
                        );
                        btnAction.IsEnabled = true;
                        btnAction.Cursor = Cursors.Hand;

                        btnAction.Click += async (s, e) =>
                        {
                            btnAction.IsEnabled = false;
                            btnAction.Content = "Đang đổi...";
                            bool success = await SkinService.RedeemSkinAsync(_config, skin, _config.GetEffectiveGameDir());
                            if (success)
                            {
                                UpdateCoinDisplay();
                                RenderSkinCards();
                                LogMessage($"[Đổi Thưởng] Mua thành công mô hình '{skin.Name}' (-{skin.Price:N0} Đá Cuội). Số dư: {_config.CobbleCoins:N0}");
                                MessageBox.Show($"Chúc mừng! Bạn đã đổi thành công skin '{skin.Name}'.\nMô hình đã sẵn sàng trong game!", "Đổi Skin Thành Công", MessageBoxButton.OK, MessageBoxImage.Information);
                            }
                            else
                            {
                                MessageBox.Show("Không đủ Đá Cuội hoặc xảy ra lỗi khi đổi skin!", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Warning);
                                RenderSkinCards();
                            }
                        };
                    }
                    else
                    {
                        btnAction.Foreground = new SolidColorBrush(Color.FromRgb(0x94, 0xA3, 0xB8));
                        btnAction.Template = CreateCustomButtonTemplate(
                            new SolidColorBrush(Color.FromArgb(0x15, 0xFF, 0xFF, 0xFF)),
                            new SolidColorBrush(Color.FromArgb(0x25, 0xFF, 0xFF, 0xFF)),
                            btnAction.Foreground, 8
                        );
                        btnAction.IsEnabled = false;
                        btnAction.Cursor = Cursors.Arrow;
                    }
                }

                System.Windows.Controls.Grid.SetRow(btnAction, 2);
                cardGrid.Children.Add(btnAction);

                cardBorder.Child = cardGrid;
                ContainerSkinCards.Children.Add(cardBorder);
            }
        }

        private void LogMessage(string message)
        {
            Dispatcher.Invoke(() =>
            {
                TxtLogs.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}\n");
                ScrollLogs.ScrollToEnd();
            });
        }
    }
}