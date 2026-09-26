using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using CobblemonLauncher.Services;

namespace GiftcodeGen
{
    public class SkinEntry
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public bool IsSelectedPerm { get; set; }
        public bool IsSelectedTrial { get; set; }
        public string TrialDuration { get; set; } = "24h";
    }

    public partial class MainWindow : Window
    {
        private readonly List<SkinEntry> _allSkins = new();
        private bool _isTrialMode = false;
        private readonly List<string> _currentGeneratedCodes = new();

        public MainWindow()
        {
            InitializeComponent();
            LoadSkinsFromPool();
            RenderSkinsList();
        }

        private void LoadSkinsFromPool()
        {
            string rootDir = FindProjectRoot();
            string testIpPath = Path.Combine(rootDir, "testip.json");

            if (File.Exists(testIpPath))
            {
                try
                {
                    string json = File.ReadAllText(testIpPath);
                    using var doc = JsonDocument.Parse(json);
                    if (doc.RootElement.TryGetProperty("skins", out var skinsElem) && skinsElem.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var item in skinsElem.EnumerateArray())
                        {
                            string id = item.TryGetProperty("id", out var idProp) ? idProp.GetString() ?? "" : "";
                            string name = item.TryGetProperty("name", out var nameProp) ? nameProp.GetString() ?? "" : "";
                            string file = item.TryGetProperty("fileName", out var fileProp) ? fileProp.GetString() ?? "" : "";

                            if (!string.IsNullOrWhiteSpace(id))
                            {
                                _allSkins.Add(new SkinEntry
                                {
                                    Id = id,
                                    Name = string.IsNullOrWhiteSpace(name) ? id : name,
                                    FileName = file
                                });
                            }
                        }
                    }
                }
                catch { }
            }

            // Fallback scan YSM_Models_Pool if testip.json yielded nothing
            if (_allSkins.Count == 0)
            {
                string poolDir = Path.Combine(rootDir, "YSM_Models_Pool");
                if (Directory.Exists(poolDir))
                {
                    foreach (var file in Directory.GetFiles(poolDir, "*.ysm"))
                    {
                        string id = Path.GetFileNameWithoutExtension(file).ToLower().Replace(" ", "_");
                        _allSkins.Add(new SkinEntry
                        {
                            Id = id,
                            Name = Path.GetFileNameWithoutExtension(file),
                            FileName = Path.GetFileName(file)
                        });
                    }
                }
            }

            TxtPoolCount.Text = $"Kho có {_allSkins.Count} skins";
        }

        private static string FindProjectRoot()
        {
            string dir = AppDomain.CurrentDomain.BaseDirectory;
            for (int i = 0; i < 6; i++)
            {
                if (File.Exists(Path.Combine(dir, "testip.json")) || Directory.Exists(Path.Combine(dir, "YSM_Models_Pool")))
                {
                    return dir;
                }
                var parent = Directory.GetParent(dir);
                if (parent == null) break;
                dir = parent.FullName;
            }
            return AppDomain.CurrentDomain.BaseDirectory;
        }

        private void RenderSkinsList()
        {
            PanelSkinsList.Children.Clear();
            string search = TxtSkinSearch.Text.Trim().ToLowerInvariant();

            var filtered = _allSkins.Where(s =>
                string.IsNullOrWhiteSpace(search) ||
                s.Id.ToLowerInvariant().Contains(search) ||
                s.Name.ToLowerInvariant().Contains(search)
            ).ToList();

            foreach (var skin in filtered)
            {
                var cb = new CheckBox
                {
                    Tag = skin,
                    IsChecked = _isTrialMode ? skin.IsSelectedTrial : skin.IsSelectedPerm,
                    Margin = new Thickness(0, 3, 0, 3)
                };

                var contentPanel = new StackPanel { Orientation = Orientation.Horizontal };
                contentPanel.Children.Add(new TextBlock
                {
                    Text = skin.Name,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = Brushes.White,
                    FontSize = 12
                });
                contentPanel.Children.Add(new TextBlock
                {
                    Text = $" ({skin.Id})",
                    Foreground = new SolidColorBrush(Color.FromRgb(0x94, 0xA3, 0xB8)),
                    FontSize = 11,
                    Margin = new Thickness(4, 1, 0, 0)
                });

                if (_isTrialMode && skin.IsSelectedTrial)
                {
                    contentPanel.Children.Add(new TextBlock
                    {
                        Text = $" [⏳ {skin.TrialDuration}]",
                        Foreground = new SolidColorBrush(Color.FromRgb(0xD8, 0xB4, 0xFE)),
                        FontWeight = FontWeights.Bold,
                        FontSize = 11,
                        Margin = new Thickness(6, 1, 0, 0)
                    });
                }

                cb.Content = contentPanel;

                cb.Checked += (s, e) =>
                {
                    if (_isTrialMode)
                    {
                        skin.IsSelectedTrial = true;
                        skin.TrialDuration = GetCurrentTrialDurationTag();
                    }
                    else
                    {
                        skin.IsSelectedPerm = true;
                    }
                    UpdateSelectedSkinsSummary();
                };

                cb.Unchecked += (s, e) =>
                {
                    if (_isTrialMode)
                    {
                        skin.IsSelectedTrial = false;
                    }
                    else
                    {
                        skin.IsSelectedPerm = false;
                    }
                    UpdateSelectedSkinsSummary();
                };

                PanelSkinsList.Children.Add(cb);
            }

            UpdateSelectedSkinsSummary();
        }

        private string GetCurrentTrialDurationTag()
        {
            if (ComboTrialDuration == null) return "24h";

            if (ComboTrialDuration.SelectedItem is ComboBoxItem item && item.Tag is string tag)
            {
                if (tag == "custom")
                {
                    if (TxtCustomTrialDays != null && double.TryParse(TxtCustomTrialDays.Text.Trim(), out double customDays) && customDays > 0)
                    {
                        return $"{customDays}d";
                    }
                    return "7d";
                }
                return tag;
            }
            return "24h";
        }

        private void ComboTrialDuration_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (PanelCustomTrial == null) return;

            bool isCustom = ComboTrialDuration.SelectedItem is ComboBoxItem item && item.Tag is string tag && tag == "custom";
            PanelCustomTrial.Visibility = isCustom ? Visibility.Visible : Visibility.Collapsed;

            string activeTag = GetCurrentTrialDurationTag();
            foreach (var s in _allSkins.Where(s => s.IsSelectedTrial))
            {
                s.TrialDuration = activeTag;
            }

            RenderSkinsList();
        }

        private void TxtCustomTrialDays_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (PanelCustomTrial != null && PanelCustomTrial.Visibility == Visibility.Visible)
            {
                string activeTag = GetCurrentTrialDurationTag();
                foreach (var s in _allSkins.Where(s => s.IsSelectedTrial))
                {
                    s.TrialDuration = activeTag;
                }
                RenderSkinsList();
            }
        }

        private static string FormatHours(double hours)
        {
            if (hours >= 24 && Math.Abs(hours % 24) < 0.01)
            {
                int days = (int)(hours / 24);
                return $"{days} ngày ({hours:0.#}h)";
            }
            return $"{hours:0.#} giờ";
        }

        private static double ParseDurationHours(string tag)
        {
            tag = tag.Trim().ToLowerInvariant();
            if (tag.EndsWith("d") && double.TryParse(tag[..^1], out double d)) return d * 24;
            if (tag.EndsWith("h") && double.TryParse(tag[..^1], out double h)) return h;
            if (tag.EndsWith("m") && double.TryParse(tag[..^1], out double m)) return m / 60.0;
            if (double.TryParse(tag, out double num)) return num;
            return 24;
        }

        private void UpdateSelectedSkinsSummary()
        {
            int permCount = _allSkins.Count(s => s.IsSelectedPerm);
            int trialCount = _allSkins.Count(s => s.IsSelectedTrial);

            if (_isTrialMode)
            {
                TxtSelectedSkinsSummary.Text = $"Đã chọn: {trialCount} skin dùng thử (Vĩnh viễn: {permCount})";
                TxtSelectedSkinsSummary.Foreground = new SolidColorBrush(Color.FromRgb(0xD8, 0xB4, 0xFE));
            }
            else
            {
                TxtSelectedSkinsSummary.Text = $"Đã chọn: {permCount} skin vĩnh viễn (Dùng thử: {trialCount})";
                TxtSelectedSkinsSummary.Foreground = new SolidColorBrush(Color.FromRgb(0x00, 0xFF, 0x9D));
            }
        }

        private void BtnSubTabPerm_Click(object sender, RoutedEventArgs e)
        {
            _isTrialMode = false;
            BtnSubTabPerm.Background = new SolidColorBrush(Color.FromRgb(0x00, 0xFF, 0x9D));
            BtnSubTabPerm.Foreground = new SolidColorBrush(Color.FromRgb(0x04, 0x2F, 0x1A));
            BtnSubTabPerm.BorderBrush = new SolidColorBrush(Color.FromRgb(0x00, 0xFF, 0x9D));

            BtnSubTabTrial.Background = new SolidColorBrush(Color.FromRgb(0x1E, 0x29, 0x3B));
            BtnSubTabTrial.Foreground = new SolidColorBrush(Color.FromRgb(0x94, 0xA3, 0xB8));
            BtnSubTabTrial.BorderBrush = new SolidColorBrush(Color.FromRgb(0x33, 0x41, 0x55));

            BorderTrialDurationSelector.Visibility = Visibility.Collapsed;
            RenderSkinsList();
        }

        private void BtnSubTabTrial_Click(object sender, RoutedEventArgs e)
        {
            _isTrialMode = true;
            BtnSubTabTrial.Background = new SolidColorBrush(Color.FromRgb(0xA8, 0x55, 0xF7));
            BtnSubTabTrial.Foreground = Brushes.White;
            BtnSubTabTrial.BorderBrush = new SolidColorBrush(Color.FromRgb(0xA8, 0x55, 0xF7));

            BtnSubTabPerm.Background = new SolidColorBrush(Color.FromRgb(0x1E, 0x29, 0x3B));
            BtnSubTabPerm.Foreground = new SolidColorBrush(Color.FromRgb(0x94, 0xA3, 0xB8));
            BtnSubTabPerm.BorderBrush = new SolidColorBrush(Color.FromRgb(0x33, 0x41, 0x55));

            BorderTrialDurationSelector.Visibility = Visibility.Visible;
            RenderSkinsList();
        }

        private void TxtSkinSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            RenderSkinsList();
        }

        private void BtnDeselectAll_Click(object sender, RoutedEventArgs e)
        {
            if (_isTrialMode)
            {
                foreach (var s in _allSkins) s.IsSelectedTrial = false;
            }
            else
            {
                foreach (var s in _allSkins) s.IsSelectedPerm = false;
            }
            RenderSkinsList();
        }

        private void BtnQuickCoin_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string tag && int.TryParse(tag, out int val))
            {
                if (val == 0)
                {
                    TxtCoins.Text = "0";
                }
                else
                {
                    if (int.TryParse(TxtCoins.Text.Trim(), out int cur))
                    {
                        TxtCoins.Text = (cur + val).ToString();
                    }
                    else
                    {
                        TxtCoins.Text = val.ToString();
                    }
                }
            }
        }

        private void BtnQuickCount_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string tag)
            {
                TxtGenerateCount.Text = tag;
            }
        }

        private void BtnGenerate_Click(object sender, RoutedEventArgs e)
        {
            if (!int.TryParse(TxtCoins.Text.Trim(), out int coins) || coins < 0)
            {
                MessageBox.Show("Vui lòng nhập số lượng Đá Cuội hợp lệ (>= 0)!", "Lỗi nhập liệu", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!int.TryParse(TxtGenerateCount.Text.Trim(), out int count) || count <= 0)
            {
                MessageBox.Show("Vui lòng nhập số lượng mã muốn tạo (>= 1)!", "Lỗi nhập liệu", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string activeDurationTag = GetCurrentTrialDurationTag();
            var permSkins = _allSkins.Where(s => s.IsSelectedPerm).Select(s => s.Id).ToList();
            var trialSkins = _allSkins.Where(s => s.IsSelectedTrial)
                .Select(s => new GiftcodeTrialSkin
                {
                    SkinId = s.Id,
                    Hours = ParseDurationHours(string.IsNullOrWhiteSpace(s.TrialDuration) ? activeDurationTag : s.TrialDuration)
                }).ToList();

            if (coins == 0 && permSkins.Count == 0 && trialSkins.Count == 0)
            {
                MessageBox.Show("Giftcode phải có ít nhất 1 phần thưởng (Đá cuội, Skin vĩnh viễn hoặc Skin dùng thử)!", "Chưa chọn phần thưởng", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            long? expiresAtUnix = null;
            DateTime? expiresUtc = null;
            switch (ComboCodeExpiry.SelectedIndex)
            {
                case 1: expiresUtc = DateTime.UtcNow.AddDays(3); break;
                case 2: expiresUtc = DateTime.UtcNow.AddDays(7); break;
                case 3: expiresUtc = DateTime.UtcNow.AddDays(30); break;
                case 4: expiresUtc = DateTime.UtcNow.AddDays(60); break;
            }

            if (expiresUtc.HasValue)
            {
                expiresAtUnix = new DateTimeOffset(expiresUtc.Value).ToUnixTimeSeconds();
            }

            string note = TxtCodeNote.Text.Trim();
            _currentGeneratedCodes.Clear();

            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"# ==========================================================");
            sb.AppendLine($"# DANH SÁCH GIFTCODE ĐÁ CUỘI MON ({DateTime.Now:yyyy-MM-dd HH:mm:ss})");
            sb.AppendLine($"# Ghi chú: {(string.IsNullOrWhiteSpace(note) ? "Không có" : note)}");
            sb.AppendLine($"# Phần thưởng: {coins:N0} Đá Cuội | Skins: {(permSkins.Count > 0 ? string.Join(", ", permSkins) : "Không có")} | Dùng thử: {(trialSkins.Count > 0 ? string.Join(", ", trialSkins.Select(t => $"{t.SkinId} ({FormatHours(t.Hours)})")) : "Không có")}");
            if (expiresUtc.HasValue)
            {
                sb.AppendLine($"# Hạn sử dụng mã: Hết hạn lúc {expiresUtc.Value.ToLocalTime():yyyy-MM-dd HH:mm:ss} (Local)");
            }
            sb.AppendLine($"# ==========================================================");
            sb.AppendLine();

            for (int i = 0; i < count; i++)
            {
                var payload = new GiftcodePayload
                {
                    CodeId = Guid.NewGuid().ToString("N"),
                    Coins = coins,
                    PermanentSkins = new List<string>(permSkins),
                    TrialSkins = new List<GiftcodeTrialSkin>(trialSkins),
                    ExpiresAtUnix = expiresAtUnix,
                    Salt = Guid.NewGuid().ToString("N")[..8]
                };

                string code = GiftcodeCore.EncryptGiftcode(payload);
                _currentGeneratedCodes.Add(code);
                sb.AppendLine(code);
            }

            TxtGeneratedCodes.Text = sb.ToString();
            TxtBadgeCount.Text = $"{count} mã";
            BadgeCodeCount.Visibility = Visibility.Visible;
            ShowTempStatus($"✔ Đã tạo {count} mã thành công!");
        }

        private void BtnCopyAll_Click(object sender, RoutedEventArgs e)
        {
            if (_currentGeneratedCodes.Count == 0)
            {
                MessageBox.Show("Chưa có mã nào được tạo để sao chép!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            string rawCodes = string.Join(Environment.NewLine, _currentGeneratedCodes);
            Clipboard.SetText(rawCodes);
            ShowTempStatus("✔ Đã chép tất cả mã vào clipboard!");
        }

        private void BtnSaveFile_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(TxtGeneratedCodes.Text))
            {
                MessageBox.Show("Chưa có danh sách mã nào để lưu!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                FileName = $"Giftcodes_{DateTime.Now:yyyyMMdd_HHmmss}.txt",
                Filter = "Text Files (*.txt)|*.txt|All Files (*.*)|*.*",
                Title = "Lưu danh sách mã Giftcode"
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    File.WriteAllText(dialog.FileName, TxtGeneratedCodes.Text);
                    ShowTempStatus("✔ Đã lưu file thành công!");
                    MessageBox.Show($"Đã lưu {_currentGeneratedCodes.Count} mã vào file:\n{dialog.FileName}", "Lưu file thành công", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Lỗi khi ghi file: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void BtnClearOutput_Click(object sender, RoutedEventArgs e)
        {
            TxtGeneratedCodes.Text = string.Empty;
            _currentGeneratedCodes.Clear();
            BadgeCodeCount.Visibility = Visibility.Collapsed;
            TxtCopyStatus.Text = string.Empty;
        }

        private void ShowTempStatus(string message)
        {
            TxtCopyStatus.Text = message;
            var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
            timer.Tick += (s, e) =>
            {
                TxtCopyStatus.Text = string.Empty;
                timer.Stop();
            };
            timer.Start();
        }

        // Mode Switching
        private void BtnTabGenerate_Click(object sender, RoutedEventArgs e)
        {
            GridTabGenerate.Visibility = Visibility.Visible;
            GridTabInspect.Visibility = Visibility.Collapsed;

            BtnTabGenerate.Background = new SolidColorBrush(Color.FromRgb(0x25, 0x63, 0xEB));
            BtnTabGenerate.BorderBrush = new SolidColorBrush(Color.FromRgb(0x3B, 0x82, 0xF6));
            BtnTabGenerate.Foreground = Brushes.White;

            BtnTabInspect.Background = new SolidColorBrush(Color.FromRgb(0x1E, 0x29, 0x3B));
            BtnTabInspect.BorderBrush = new SolidColorBrush(Color.FromRgb(0x33, 0x41, 0x55));
            BtnTabInspect.Foreground = new SolidColorBrush(Color.FromRgb(0xF8, 0xFA, 0xFC));
        }

        private void BtnTabInspect_Click(object sender, RoutedEventArgs e)
        {
            GridTabGenerate.Visibility = Visibility.Collapsed;
            GridTabInspect.Visibility = Visibility.Visible;

            BtnTabInspect.Background = new SolidColorBrush(Color.FromRgb(0x25, 0x63, 0xEB));
            BtnTabInspect.BorderBrush = new SolidColorBrush(Color.FromRgb(0x3B, 0x82, 0xF6));
            BtnTabInspect.Foreground = Brushes.White;

            BtnTabGenerate.Background = new SolidColorBrush(Color.FromRgb(0x1E, 0x29, 0x3B));
            BtnTabGenerate.BorderBrush = new SolidColorBrush(Color.FromRgb(0x33, 0x41, 0x55));
            BtnTabGenerate.Foreground = new SolidColorBrush(Color.FromRgb(0xF8, 0xFA, 0xFC));
        }

        // Inspect Code
        private void BtnInspectCode_Click(object sender, RoutedEventArgs e)
        {
            string code = TxtInspectInput.Text.Trim();
            if (string.IsNullOrWhiteSpace(code))
            {
                MessageBox.Show("Vui lòng nhập mã Giftcode cần kiểm tra!", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                var payload = GiftcodeCore.DecryptGiftcode(code);
                BorderInspectResult.Visibility = Visibility.Visible;

                bool isExpired = payload.ExpiresAtUnix.HasValue &&
                                 DateTimeOffset.UtcNow.ToUnixTimeSeconds() > payload.ExpiresAtUnix.Value;

                if (isExpired)
                {
                    TxtInspectStatusIcon.Text = "⏳";
                    TxtInspectStatusIcon.Foreground = new SolidColorBrush(Color.FromRgb(0xEF, 0x44, 0x44));
                    TxtInspectStatusText.Text = "MÃ ĐÃ HẾT HẠN SỬ DỤNG!";
                    TxtInspectStatusText.Foreground = new SolidColorBrush(Color.FromRgb(0xEF, 0x44, 0x44));
                }
                else
                {
                    TxtInspectStatusIcon.Text = "✔";
                    TxtInspectStatusIcon.Foreground = new SolidColorBrush(Color.FromRgb(0x00, 0xFF, 0x9D));
                    TxtInspectStatusText.Text = "MÃ HỢP LỆ (SẴN SÀNG SỬ DỤNG)";
                    TxtInspectStatusText.Foreground = new SolidColorBrush(Color.FromRgb(0x00, 0xFF, 0x9D));
                }

                TxtInspectCoins.Text = $"{payload.Coins:N0} 🪨";

                TxtInspectPermSkins.Text = (payload.PermanentSkins != null && payload.PermanentSkins.Count > 0)
                    ? string.Join(", ", payload.PermanentSkins)
                    : "Không có";

                TxtInspectTrialSkins.Text = (payload.TrialSkins != null && payload.TrialSkins.Count > 0)
                    ? string.Join(", ", payload.TrialSkins.Select(t => $"{t.SkinId} (thời hạn {FormatHours(t.Hours)})"))
                    : "Không có";

                if (payload.ExpiresAtUnix.HasValue)
                {
                    var expDate = DateTimeOffset.FromUnixTimeSeconds(payload.ExpiresAtUnix.Value).LocalDateTime;
                    TxtInspectExpiry.Text = $"{expDate:yyyy-MM-dd HH:mm:ss} (Local)";
                }
                else
                {
                    TxtInspectExpiry.Text = "Vĩnh viễn (Không giới hạn)";
                }

                TxtInspectId.Text = $"ID: {payload.CodeId} | Salt: {payload.Salt}";
            }
            catch (Exception ex)
            {
                BorderInspectResult.Visibility = Visibility.Visible;
                TxtInspectStatusIcon.Text = "❌";
                TxtInspectStatusIcon.Foreground = new SolidColorBrush(Color.FromRgb(0xEF, 0x44, 0x44));
                TxtInspectStatusText.Text = "MÃ KHÔNG HỢP LỆ HOẶC BỊ HỎNG!";
                TxtInspectStatusText.Foreground = new SolidColorBrush(Color.FromRgb(0xEF, 0x44, 0x44));

                TxtInspectCoins.Text = "0 🪨";
                TxtInspectPermSkins.Text = "Lỗi";
                TxtInspectTrialSkins.Text = "Lỗi";
                TxtInspectExpiry.Text = ex.Message;
                TxtInspectId.Text = "";
            }
        }
    }
}
