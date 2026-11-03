using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using BatchDeleteFTP.Models;
using BatchDeleteFTP.Services;
using BatchDeleteFTP.Views;
using MaterialDesignThemes.Wpf;
using Microsoft.Win32;

namespace BatchDeleteFTP;

public partial class MainWindow : Window
{
    private readonly ConfigService _configService;
    private readonly AppLogger _logger;
    private readonly FtpCleanupService _ftpService;
    private readonly EmailNotificationService _emailService;
    private readonly ObservableCollection<FtpFileCandidate> _candidates = new();
    private readonly ObservableCollection<FileManagerEntry> _fmEntries = new();
    private readonly ObservableCollection<FileManagerEntry> _fmDisplayEntries = new();
    private string _currentFmRemotePath = "/";
    private bool _fmLoadedOnce;
    private FtpSettings _currentSettings = new();
    private bool _suppressProfileSelectionChanged;
    private bool _suppressThemeChanged;
    private readonly List<string> _selectedScanProfileNames = new();

    public MainWindow() : this(new ConfigService(), new AppLogger())
    {
    }

    private MainWindow(ConfigService configService, AppLogger logger)
        : this(configService, logger, new FtpCleanupService(logger), new EmailNotificationService(logger))
    {
    }

    public MainWindow(ConfigService configService, AppLogger logger, FtpCleanupService ftpService, EmailNotificationService? emailService = null)
    {
        InitializeComponent();
        this.WindowState = System.Windows.WindowState.Maximized;
        _configService = configService;
        _logger = logger;
        _ftpService = ftpService;
        _emailService = emailService ?? new EmailNotificationService(logger);

        GridCandidates.ItemsSource = _candidates;
        GridFileManager.ItemsSource = _fmDisplayEntries;

        _logger.OnLogMessage += line =>
        {
            Dispatcher.Invoke(() =>
            {
                TxtLogs.AppendText(line + Environment.NewLine);
                TxtLogs.ScrollToEnd();
            });
        };

        _currentSettings = _configService.Load();
        if (!string.IsNullOrWhiteSpace(_currentSettings.SelectedProfileName))
        {
            _selectedScanProfileNames.Add(_currentSettings.SelectedProfileName);
        }
        CboThemes.ItemsSource = ThemeService.AvailableThemes;
        RefreshProfileComboBox(_currentSettings.SelectedProfileName);
        LoadSettingsToUi(_currentSettings);
        UpdateScanProfilesButtonLabel();
        var initialTheme = ThemeService.ParseTheme(_currentSettings.ThemeName, _currentSettings.IsDarkTheme);
        ApplyTheme(initialTheme);        
    }

    public void SetDarkTheme(bool isDark) =>
        SetTheme(isDark ? AppThemeKind.MaterialDark : AppThemeKind.MaterialLight);

    public void SetTheme(AppThemeKind kind)
    {
        _currentSettings.ThemeName = kind.ToString();
        _currentSettings.IsDarkTheme = ThemeService.GetThemeOption(kind).IsDarkBase;
        ApplyTheme(kind);
    }

    private void ApplyTheme(AppThemeKind kind)
    {
        _suppressThemeChanged = true;
        try
        {
            ThemeService.ApplyTheme(kind);
            var option = ThemeService.GetThemeOption(kind);
            CboThemes.SelectedItem = option;

            if (Enum.TryParse<PackIconKind>(option.IconKind, out var parsedIcon))
            {
                IconThemeMode.Kind = parsedIcon;
            }
        }
        finally
        {
            _suppressThemeChanged = false;
        }
    }

    private void CboThemes_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressThemeChanged) return;
        if (CboThemes.SelectedItem is not AppThemeOption selectedTheme) return;

        _currentSettings.ThemeName = selectedTheme.Kind.ToString();
        _currentSettings.IsDarkTheme = selectedTheme.IsDarkBase;
        ApplyTheme(selectedTheme.Kind);

        try
        {
            _configService.Save(_currentSettings);
            _logger.Info($"Đã chuyển giao diện sang Theme: '{selectedTheme.DisplayName}'.");
        }
        catch (Exception ex)
        {
            _logger.Error($"Không thể lưu cấu hình Theme: {ex.Message}");
        }
    }
    private void RefreshProfileComboBox(string? selectedProfileName)
    {
        _suppressProfileSelectionChanged = true;
        try
        {
            CboProfiles.ItemsSource = null;
            CboProfiles.ItemsSource = _currentSettings.Profiles;
            var matched = _currentSettings.Profiles.FirstOrDefault(p =>
                string.Equals(p.Name, selectedProfileName, StringComparison.OrdinalIgnoreCase))
                ?? _currentSettings.Profiles.FirstOrDefault();

            if (matched != null)
            {
                CboProfiles.SelectedItem = matched;
                CboProfiles.Text = matched.Name;
            }
            else if (!string.IsNullOrWhiteSpace(selectedProfileName))
            {
                CboProfiles.Text = selectedProfileName;
            }

            _selectedScanProfileNames.RemoveAll(name => !_currentSettings.Profiles.Any(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase)));
            if (_selectedScanProfileNames.Count == 0 && !string.IsNullOrWhiteSpace(_currentSettings.SelectedProfileName))
            {
                _selectedScanProfileNames.Add(_currentSettings.SelectedProfileName);
            }
            UpdateScanProfilesButtonLabel();
        }
        finally
        {
            _suppressProfileSelectionChanged = false;
        }
    }

    private void LoadSettingsToUi(FtpSettings s)
    {
        string hostPort = $"{s.Host}:{s.Port} ({s.EncryptionMode})";
        TxtSummaryHost.Text = hostPort;
        TxtSummaryUser.Text = string.IsNullOrWhiteSpace(s.Username) ? "(Chưa đặt)" : s.Username;
        TxtSummaryProxy.Text = s.UseProxy ? $"{s.ProxyHost}:{s.ProxyPort}" : "Tắt";
        TxtSummaryRemotePaths.Text = s.RemotePaths != null && s.RemotePaths.Count > 0 ? string.Join(";", s.RemotePaths) : "/";
        TxtSummaryRetention.Text = $"+{s.RetentionDays} ngày";
        string backupStatus = s.EnableBackup
            ? (string.IsNullOrWhiteSpace(s.LocalBackupPath) ? "Bật" : $"Bật ({System.IO.Path.GetFileName(s.LocalBackupPath)})")
            : "Tắt";
        TxtSummaryBackup.Text = backupStatus;
    }

    private FtpSettings ReadSettingsFromUi()
    {
        return _currentSettings;
    }

    private void BtnOpenConnectionSettings_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Views.ConnectionSettingsWindow(_currentSettings, _configService, _ftpService, _logger)
        {
            Owner = this
        };

        if (dialog.ShowDialog() == true)
        {
            RefreshProfileComboBox(_currentSettings.SelectedProfileName);
            LoadSettingsToUi(_currentSettings);
            _logger.Info($"Đã cập nhật và áp dụng cấu hình Profile '{_currentSettings.SelectedProfileName}'.");
        }
    }

    private void CboProfiles_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressProfileSelectionChanged) return;
        if (CboProfiles.SelectedItem is FtpSiteProfile selectedProfile)
        {
            selectedProfile.ApplyTo(_currentSettings);
            LoadSettingsToUi(_currentSettings);
            if (_selectedScanProfileNames.Count <= 1)
            {
                _selectedScanProfileNames.Clear();
                _selectedScanProfileNames.Add(selectedProfile.Name);
                UpdateScanProfilesButtonLabel();
            }
            _logger.Info($"Đã chuyển sang Profile: '{selectedProfile.Name}' ({selectedProfile.Username}@{selectedProfile.Host}:{selectedProfile.Port}).");
        }
    }

    private void BtnNewProfile_Click(object sender, RoutedEventArgs e)
    {
        int index = _currentSettings.Profiles.Count + 1;
        string newName = $"Site_{index}";
        while (_currentSettings.Profiles.Any(p => string.Equals(p.Name, newName, StringComparison.OrdinalIgnoreCase)))
        {
            index++;
            newName = $"Site_{index}";
        }

        _suppressProfileSelectionChanged = true;
        try
        {
            CboProfiles.SelectedItem = null;
            CboProfiles.Text = newName;
        }
        finally
        {
            _suppressProfileSelectionChanged = false;
        }

        CboProfiles.Focus();
        _logger.Info($"Đang tạo Profile mới '{newName}'. Hãy chỉnh sửa thông tin và nhấn 'Lưu Profile'.");
    }

    private void BtnSaveProfile_Click(object sender, RoutedEventArgs e)
    {
        string profileName = CboProfiles.Text.Trim();
        if (string.IsNullOrWhiteSpace(profileName))
        {
            MessageBox.Show("Vui lòng nhập tên Profile trước khi lưu.", "Thiếu tên Profile", MessageBoxButton.OK, MessageBoxImage.Warning);
            CboProfiles.Focus();
            return;
        }

        _configService.UpsertProfile(_currentSettings, profileName);
        _configService.Save(_currentSettings);

        RefreshProfileComboBox(profileName);
        LoadSettingsToUi(_currentSettings);
        _logger.Info($"Đã lưu Profile '{profileName}' vào '{_configService.DefaultConfigPath}'.");
        MessageBox.Show($"Đã lưu Profile '{profileName}' thành công!", "Thành công", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void BtnDeleteProfile_Click(object sender, RoutedEventArgs e)
    {
        string targetName = (CboProfiles.SelectedItem as FtpSiteProfile)?.Name ?? CboProfiles.Text.Trim();
        if (string.IsNullOrWhiteSpace(targetName))
        {
            return;
        }

        var confirm = MessageBox.Show(
            $"Bạn có chắc chắn muốn xóa Profile '{targetName}' không?",
            "Xác nhận xóa Profile",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (confirm != MessageBoxResult.Yes) return;

        if (_configService.DeleteProfile(_currentSettings, targetName))
        {
            _configService.Save(_currentSettings);
            RefreshProfileComboBox(_currentSettings.SelectedProfileName);
            LoadSettingsToUi(_currentSettings);
            _logger.Info($"Đã xóa Profile '{targetName}'. Đang chọn: '{_currentSettings.SelectedProfileName}'.");
        }
    }

    private void UpdateScanProfilesButtonLabel()
    {
        if (_selectedScanProfileNames.Count == 0 && !string.IsNullOrWhiteSpace(_currentSettings.SelectedProfileName))
        {
            _selectedScanProfileNames.Add(_currentSettings.SelectedProfileName);
        }

        int totalProfiles = _currentSettings.Profiles.Count;
        if (totalProfiles > 1 && _selectedScanProfileNames.Count >= totalProfiles)
        {
            TxtScanProfilesLabel.Text = $"Quét: Tất cả ({totalProfiles})";
        }
        else if (_selectedScanProfileNames.Count == 1)
        {
            TxtScanProfilesLabel.Text = $"Quét: {_selectedScanProfileNames[0]}";
        }
        else
        {
            TxtScanProfilesLabel.Text = $"Quét: {_selectedScanProfileNames.Count} Profiles";
        }
    }

    private void BtnSelectScanProfiles_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Views.SelectProfilesWindow(_currentSettings.Profiles, _selectedScanProfileNames)
        {
            Owner = this
        };

        if (dialog.ShowDialog() == true)
        {
            _selectedScanProfileNames.Clear();
            _selectedScanProfileNames.AddRange(dialog.SelectedProfileNames);
            UpdateScanProfilesButtonLabel();
            _logger.Info($"Đã chọn {_selectedScanProfileNames.Count} profile để quét: {string.Join(", ", _selectedScanProfileNames)}");
        }
    }

    private List<FtpSiteProfile> GetTargetScanProfiles()
    {
        var result = new List<FtpSiteProfile>();
        var selectedSet = new HashSet<string>(_selectedScanProfileNames, StringComparer.OrdinalIgnoreCase);

        foreach (var prof in _currentSettings.Profiles)
        {
            if (selectedSet.Contains(prof.Name))
            {
                result.Add(prof);
            }
        }

        if (result.Count == 0)
        {
            var matched = _currentSettings.Profiles.FirstOrDefault(p =>
                string.Equals(p.Name, _currentSettings.SelectedProfileName, StringComparison.OrdinalIgnoreCase));
            if (matched != null)
            {
                result.Add(matched);
            }
            else
            {
                result.Add(FtpSiteProfile.FromSettings(_currentSettings.SelectedProfileName, _currentSettings));
            }
        }

        return result;
    }

    private void SetUiBusy(bool isBusy, string? busyText = null)
    {
        CboProfiles.IsEnabled = !isBusy;
        BtnNewProfile.IsEnabled = !isBusy;
        BtnSaveProfile.IsEnabled = !isBusy;
        BtnDeleteProfile.IsEnabled = !isBusy;
        BtnOpenConnectionSettings.IsEnabled = !isBusy;
        BtnSelectScanProfiles.IsEnabled = !isBusy;
        BtnQuickTestConnection.IsEnabled = !isBusy;
        BtnTestConnection.IsEnabled = !isBusy;
        BtnPreview.IsEnabled = !isBusy;
        BtnSendPreviewEmail.IsEnabled = !isBusy;
        BtnEmailSettings.IsEnabled = !isBusy;
        BtnExecuteDelete.IsEnabled = !isBusy;
        BusyProgressBar.Visibility = isBusy ? Visibility.Visible : Visibility.Collapsed;

        if (isBusy)
        {
            Mouse.OverrideCursor = Cursors.Wait;
            if (!string.IsNullOrEmpty(busyText))
            {
                TxtTestConnectionLabel.Text = busyText;
                TxtQuickTestConnectionLabel.Text = busyText;
            }
        }
        else
        {
            Mouse.OverrideCursor = null;
            TxtTestConnectionLabel.Text = "Kiểm Tra Kết Nối";
            TxtQuickTestConnectionLabel.Text = "Kiểm Tra Kết Nối";
        }
    }

    private void BtnSaveConfig_Click(object sender, RoutedEventArgs e)
    {
        _configService.Save(_currentSettings);
        RefreshProfileComboBox(_currentSettings.SelectedProfileName);
        LoadSettingsToUi(_currentSettings);
        _logger.Info($"Đã lưu cấu hình (Profile '{_currentSettings.SelectedProfileName}') vào '{_configService.DefaultConfigPath}'.");
        MessageBox.Show($"Đã lưu cấu hình cho Profile '{_currentSettings.SelectedProfileName}' thành công!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private async void BtnTestConnection_Click(object sender, RoutedEventArgs e)
    {
        SetUiBusy(true, "Đang kết nối...");
        try
        {
            var settings = ReadSettingsFromUi();
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            await Task.Run(() => _ftpService.TestConnectionAsync(settings, cts.Token));
            MessageBox.Show("Kết nối tới FTP Server thành công!", "Thành công", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (OperationCanceledException)
        {
            _logger.Error("Kết nối thất bại: Quá thời gian chờ (Timeout 30s). Vui lòng kiểm tra lại Proxy hoặc đường truyền mạng.");
            MessageBox.Show("Kết nối thất bại: Quá thời gian chờ (Timeout 30s). Vui lòng kiểm tra lại cấu hình Proxy hoặc đường truyền mạng.", "Hết thời gian chờ", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        catch (Exception ex)
        {
            string details = ex.InnerException != null ? $"{ex.Message}\nChi tiết: {ex.InnerException.Message}" : ex.Message;
            _logger.Error($"Kết nối thất bại: {details}");
            MessageBox.Show($"Kết nối thất bại:\n{details}", "Lỗi Kết Nối", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            SetUiBusy(false);
        }
    }



    private async void BtnPreview_Click(object sender, RoutedEventArgs e)
    {
        SetUiBusy(true);
        try
        {
            var targetProfiles = GetTargetScanProfiles();
            _candidates.Clear();

            int successfulProfiles = 0;
            var failedProfiles = new List<string>();

            _logger.Info($"Bắt đầu quét xem trước trên {targetProfiles.Count} Site Profile...");

            foreach (var profile in targetProfiles)
            {
                var profSettings = profile.ToSettings(_currentSettings);
                _logger.Info($"--- [Profile: {profile.Name}] Bắt đầu quét ({profile.Host}:{profile.Port}) ---");

                try
                {
                    var list = await _ftpService.ScanCandidatesAsync(profSettings, DateTime.Now);
                    foreach (var item in list)
                    {
                        item.ProfileName = profile.Name;
                        _candidates.Add(item);
                    }
                    successfulProfiles++;
                    _logger.Info($"[Profile: {profile.Name}] Quét thành công! Tìm thấy {list.Count} file thỏa điều kiện.");
                }
                catch (Exception exProf)
                {
                    _logger.Error($"[Profile: {profile.Name}] Quét thất bại: {exProf.Message}");
                    failedProfiles.Add($"{profile.Name}: {exProf.Message}");
                }
            }

            _logger.Info($"Tổng kết Xem Trước: Quét thành công {successfulProfiles}/{targetProfiles.Count} profile, tổng cộng {_candidates.Count} file.");

            if (failedProfiles.Count > 0)
            {
                string failDetails = string.Join("\n- ", failedProfiles);
                MessageBox.Show($"Quét hoàn tất với một số cảnh báo:\n- Thành công: {successfulProfiles}/{targetProfiles.Count} profile (tìm thấy {_candidates.Count} file)\n- Thất bại ({failedProfiles.Count} profile):\n- {failDetails}",
                    "Cảnh báo quét Profile", MessageBoxButton.OK, MessageBoxImage.Warning);
            }

            if (_currentSettings.AutoSendEmailOnPreview && _currentSettings.RecipientEmails.Count > 0)
            {
                await _emailService.SendPreviewReportAsync(_currentSettings, _candidates);
            }
        }
        catch (Exception ex)
        {
            _logger.Error($"Lỗi khi Xem Trước: {ex.Message}");
            MessageBox.Show($"Lỗi khi quét file hoặc gửi email Xem Trước:\n{ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            SetUiBusy(false);
        }
    }

    private void BtnEmailSettings_Click(object sender, RoutedEventArgs e)
    {
        _currentSettings = ReadSettingsFromUi();
        var dialog = new EmailSettingsWindow(_currentSettings, _emailService, _candidates)
        {
            Owner = this
        };

        if (dialog.ShowDialog() == true)
        {
            _configService.Save(_currentSettings);
            _logger.Info($"Đã lưu cấu hình Email (SMTP: {_currentSettings.SmtpHost}:{_currentSettings.SmtpPort}, Số email nhận: {_currentSettings.RecipientEmails.Count}).");
        }
    }

    private async void BtnSendPreviewEmail_Click(object sender, RoutedEventArgs e)
    {
        _currentSettings = ReadSettingsFromUi();
        _currentSettings.RecipientEmails ??= new List<string>();

        if (_currentSettings.RecipientEmails.Count == 0)
        {
            var setupDialog = new EmailSettingsWindow(_currentSettings, _emailService, _candidates)
            {
                Owner = this
            };

            if (setupDialog.ShowDialog() != true || _currentSettings.RecipientEmails.Count == 0)
            {
                return;
            }

            _configService.Save(_currentSettings);
        }

        SetUiBusy(true);
        try
        {
            if (_candidates.Count == 0)
            {
                var confirmScan = MessageBox.Show(
                    "Danh sách Xem Trước hiện đang trống. Bạn có muốn quét danh sách file trên FTP Server ngay bây giờ rồi gửi Email không?",
                    "Quét Xem Trước & Gửi Email",
                    MessageBoxButton.YesNoCancel,
                    MessageBoxImage.Question);

                if (confirmScan == MessageBoxResult.Cancel) return;

                if (confirmScan == MessageBoxResult.Yes)
                {
                    var targetProfiles = GetTargetScanProfiles();
                    foreach (var profile in targetProfiles)
                    {
                        var profSettings = profile.ToSettings(_currentSettings);
                        try
                        {
                            var list = await _ftpService.ScanCandidatesAsync(profSettings, DateTime.Now);
                            foreach (var item in list)
                            {
                                item.ProfileName = profile.Name;
                                _candidates.Add(item);
                            }
                        }
                        catch (Exception exProf)
                        {
                            _logger.Error($"[Profile: {profile.Name}] Quét thất bại: {exProf.Message}");
                        }
                    }
                }
            }

            await _emailService.SendPreviewReportAsync(_currentSettings, _candidates);
            MessageBox.Show(
                $"Đã gửi kết quả Xem Trước ({_candidates.Count(c => c.IsSelected)} file) thành công tới {_currentSettings.RecipientEmails.Count} địa chỉ email:\n- {string.Join("\n- ", _currentSettings.RecipientEmails)}",
                "Gửi Email Thành Công",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            _logger.Error($"Lỗi khi gửi Email Xem Trước: {ex.Message}");
            MessageBox.Show($"Gửi Email Xem Trước thất bại:\n{ex.Message}", "Lỗi Gửi Email", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            SetUiBusy(false);
        }
    }

    private async void BtnExecuteDelete_Click(object sender, RoutedEventArgs e)
    {
        SetUiBusy(true);
        try
        {
            if (_candidates.Count == 0)
            {
                var targetProfiles = GetTargetScanProfiles();
                foreach (var profile in targetProfiles)
                {
                    var profSettings = profile.ToSettings(_currentSettings);
                    try
                    {
                        var scanned = await _ftpService.ScanCandidatesAsync(profSettings, DateTime.Now);
                        foreach (var item in scanned)
                        {
                            item.ProfileName = profile.Name;
                            _candidates.Add(item);
                        }
                    }
                    catch (Exception exProf)
                    {
                        _logger.Error($"[Profile: {profile.Name}] Quét thất bại: {exProf.Message}");
                    }
                }
            }

            var selectedCandidates = _candidates.Where(c => c.IsSelected).ToList();
            if (selectedCandidates.Count == 0)
            {
                MessageBox.Show("Không có file nào thỏa điều kiện hoặc được chọn để xóa.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var groups = selectedCandidates.GroupBy(c => string.IsNullOrWhiteSpace(c.ProfileName) ? _currentSettings.SelectedProfileName : c.ProfileName).ToList();

            string actionDesc = $"thực hiện Sao lưu & Xóa {selectedCandidates.Count} file trên {groups.Count} Site Profile";
            var confirm = MessageBox.Show(
                $"Bạn có chắc chắn muốn {actionDesc} không?\n\nChi tiết các Profile sẽ xử lý:\n" +
                string.Join("\n", groups.Select(g => $"- Profile '{g.Key}': {g.Count()} file")),
                "Xác nhận xóa file đa Profile",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (confirm != MessageBoxResult.Yes) return;

            int totalDeleted = 0;
            int totalFailed = 0;

            foreach (var group in groups)
            {
                var profile = _currentSettings.Profiles.FirstOrDefault(p =>
                    string.Equals(p.Name, group.Key, StringComparison.OrdinalIgnoreCase));
                var profSettings = profile != null ? profile.ToSettings(_currentSettings) : _currentSettings;

                _logger.Info($"=== Bắt đầu xử lý {group.Count()} file cho Profile '{group.Key}' ({profSettings.Host}:{profSettings.Port}) ===");
                var (deleted, failed) = await _ftpService.ExecuteBackupAndDeleteAsync(profSettings, group);
                totalDeleted += deleted;
                totalFailed += failed;
            }

            MessageBox.Show($"Hoàn tất xử lý trên {groups.Count} Profile!\n- Đã xóa thành công: {totalDeleted} file\n- Lỗi: {totalFailed} file",
                "Kết quả Thực thi", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            _logger.Error($"Lỗi khi thực thi xóa: {ex.Message}");
            MessageBox.Show($"Lỗi khi xóa file:\n{ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            SetUiBusy(false);
        }
    }

    private void BtnSelectAll_Click(object sender, RoutedEventArgs e)
    {
        foreach (var item in _candidates) item.IsSelected = true;
    }

    private void BtnUnselectAll_Click(object sender, RoutedEventArgs e)
    {
        foreach (var item in _candidates) item.IsSelected = false;
    }

    // =========================================================================
    // SECTION: FTP FILE MANAGER TAB LOGIC (VIEW, UPLOAD, DOWNLOAD, DELETE, ETC.)
    // =========================================================================

    private async void MainTabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (e.Source != MainTabControl) return;
        if (MainTabControl.SelectedItem == TabFileManager && !_fmLoadedOnce)
        {
            _fmLoadedOnce = true;
            string initialPath = _currentSettings.RemotePaths?.FirstOrDefault() ?? "/";
            if (string.IsNullOrWhiteSpace(initialPath)) initialPath = "/";
            TxtFmRemotePath.Text = initialPath;
            await LoadFileManagerDirectoryAsync(initialPath);
        }
    }

    private void SetFmBusy(bool isBusy)
    {
        FmProgressBar.Visibility = isBusy ? Visibility.Visible : Visibility.Collapsed;
        BtnFmGo.IsEnabled = !isBusy;
        BtnFmUp.IsEnabled = !isBusy;
        BtnFmHome.IsEnabled = !isBusy;
        BtnFmRefresh.IsEnabled = !isBusy;
        BtnFmUploadFile.IsEnabled = !isBusy;
        BtnFmUploadFolder.IsEnabled = !isBusy;
        BtnFmDownload.IsEnabled = !isBusy;
        BtnFmView.IsEnabled = !isBusy;
        BtnFmNewFolder.IsEnabled = !isBusy;
        BtnFmDelete.IsEnabled = !isBusy;
        GridFileManager.IsEnabled = !isBusy;

        Mouse.OverrideCursor = isBusy ? Cursors.Wait : null;
    }

    private async Task LoadFileManagerDirectoryAsync(string remotePath)
    {
        string target = string.IsNullOrWhiteSpace(remotePath) ? "/" : FtpCleanupService.NormalizeSingleRemotePath(remotePath);
        var settings = ReadSettingsFromUi();

        if (string.IsNullOrWhiteSpace(settings.Host))
        {
            MessageBox.Show(this, "Vui lòng nhập địa chỉ FTP Host trước khi duyệt thư mục.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        SetFmBusy(true);
        try
        {
            _logger.Info($"[File Manager] Đang tải danh sách thư mục: '{target}'...");
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            var entries = await _ftpService.GetDirectoryEntriesAsync(settings, target, cts.Token);

            _currentFmRemotePath = target;
            TxtFmRemotePath.Text = target;

            _fmEntries.Clear();
            foreach (var entry in entries)
            {
                entry.PropertyChanged += (_, propArgs) =>
                {
                    if (propArgs.PropertyName == nameof(FileManagerEntry.IsSelected))
                    {
                        UpdateFmSummary();
                    }
                };
                _fmEntries.Add(entry);
            }

            ApplyFmFilter();
            _logger.Info($"[File Manager] Đã tải xong '{target}' ({entries.Count} mục).");
        }
        catch (OperationCanceledException)
        {
            _logger.Error($"[File Manager] Hết thời gian chờ (Timeout 30s) khi tải '{target}'.");
            MessageBox.Show(this, "Quá thời gian chờ khi kết nối hoặc đọc danh sách file trên FTP Server (Timeout 30s).", "Timeout", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        catch (Exception ex)
        {
            string msg = ex.InnerException != null ? $"{ex.Message} ({ex.InnerException.Message})" : ex.Message;
            _logger.Error($"[File Manager] Lỗi khi tải thư mục '{target}': {msg}");
            MessageBox.Show(this, $"Không thể đọc danh sách file tại '{target}':\n{msg}", "Lỗi FTP", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            SetFmBusy(false);
            UpdateFmSummary();
        }
    }

    private void ApplyFmFilter()
    {
        string filter = TxtFmFilter?.Text?.Trim() ?? string.Empty;
        _fmDisplayEntries.Clear();

        foreach (var entry in _fmEntries)
        {
            if (entry.IsParentEntry)
            {
                _fmDisplayEntries.Add(entry);
                continue;
            }

            if (string.IsNullOrEmpty(filter) || entry.Name.Contains(filter, StringComparison.OrdinalIgnoreCase))
            {
                _fmDisplayEntries.Add(entry);
            }
        }
        UpdateFmSummary();
    }

    private void UpdateFmSummary()
    {
        if (TxtFmSummary == null) return;
        var validEntries = _fmDisplayEntries.Where(e => !e.IsParentEntry).ToList();
        int dirCount = validEntries.Count(e => e.IsDirectory);
        int fileCount = validEntries.Count(e => !e.IsDirectory);
        long totalSize = validEntries.Where(e => !e.IsDirectory).Sum(e => e.Size);
        int selectedCount = validEntries.Count(e => e.IsSelected);

        TxtFmSummary.Text = $"Tổng cộng: {validEntries.Count} mục ({dirCount} thư mục, {fileCount} file - {FileManagerEntry.FormatFileSize(totalSize)}) | Đã chọn: {selectedCount} mục";
    }

    private async void GridFileManager_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (GridFileManager.SelectedItem is not FileManagerEntry entry) return;
        if (entry.IsParentEntry || entry.IsDirectory)
        {
            await LoadFileManagerDirectoryAsync(entry.FullPath);
        }
        else
        {
            await ViewRemoteFileAsync(entry);
        }
    }

    private async void BtnFmGo_Click(object sender, RoutedEventArgs e)
    {
        await LoadFileManagerDirectoryAsync(TxtFmRemotePath.Text.Trim());
    }

    private async void TxtFmRemotePath_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            await LoadFileManagerDirectoryAsync(TxtFmRemotePath.Text.Trim());
        }
    }

    private async void BtnFmUp_Click(object sender, RoutedEventArgs e)
    {
        string parent = FtpCleanupService.GetParentRemotePath(_currentFmRemotePath);
        await LoadFileManagerDirectoryAsync(parent);
    }

    private async void BtnFmHome_Click(object sender, RoutedEventArgs e)
    {
        await LoadFileManagerDirectoryAsync("/");
    }

    private async void BtnFmRefresh_Click(object sender, RoutedEventArgs e)
    {
        await LoadFileManagerDirectoryAsync(_currentFmRemotePath);
    }

    private void TxtFmFilter_TextChanged(object sender, TextChangedEventArgs e)
    {
        ApplyFmFilter();
    }

    private void BtnFmSelectAll_Click(object sender, RoutedEventArgs e)
    {
        foreach (var entry in _fmDisplayEntries)
        {
            if (!entry.IsParentEntry) entry.IsSelected = true;
        }
    }

    private void BtnFmUnselectAll_Click(object sender, RoutedEventArgs e)
    {
        foreach (var entry in _fmDisplayEntries)
        {
            entry.IsSelected = false;
        }
    }

    private List<FileManagerEntry> GetSelectedOrActiveEntries()
    {
        var checkedItems = _fmDisplayEntries.Where(e => e.IsSelected && !e.IsParentEntry).ToList();
        if (checkedItems.Count > 0) return checkedItems;

        if (GridFileManager.SelectedItem is FileManagerEntry active && !active.IsParentEntry)
        {
            return new List<FileManagerEntry> { active };
        }

        return new List<FileManagerEntry>();
    }

    private async Task ViewRemoteFileAsync(FileManagerEntry entry)
    {
        if (entry.IsParentEntry || entry.IsDirectory)
        {
            await LoadFileManagerDirectoryAsync(entry.FullPath);
            return;
        }

        var settings = ReadSettingsFromUi();
        SetFmBusy(true);
        entry.Status = "Đang tải xem...";
        try
        {
            string tempDir = Path.Combine(Path.GetTempPath(), "BatchDeleteFTP", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);
            string localFile = Path.Combine(tempDir, entry.Name);

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
            bool ok = await _ftpService.DownloadSingleFileAsync(settings, entry.FullPath, localFile, cts.Token);
            if (ok && File.Exists(localFile))
            {
                entry.Status = "Đã mở";
                var viewer = new FilePreviewWindow(entry, localFile)
                {
                    Owner = this
                };
                viewer.ShowDialog();
            }
            else
            {
                entry.Status = "Lỗi tải file";
                MessageBox.Show(this, $"Không thể tải file '{entry.Name}' để xem trước.", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        catch (Exception ex)
        {
            entry.Status = "Lỗi";
            _logger.Error($"[File Manager] Lỗi khi xem file '{entry.FullPath}': {ex.Message}");
            MessageBox.Show(this, $"Lỗi khi tải xem file:\n{ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            SetFmBusy(false);
        }
    }

    private async void BtnFmView_Click(object sender, RoutedEventArgs e)
    {
        var items = GetSelectedOrActiveEntries();
        if (items.Count == 0)
        {
            MessageBox.Show(this, "Vui lòng chọn một file để xem.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        await ViewRemoteFileAsync(items[0]);
    }

    private void CtxFmView_Click(object sender, RoutedEventArgs e) => BtnFmView_Click(sender, e);

    private async void BtnFmDownload_Click(object sender, RoutedEventArgs e)
    {
        var items = GetSelectedOrActiveEntries();
        if (items.Count == 0)
        {
            MessageBox.Show(this, "Vui lòng chọn ít nhất một file hoặc thư mục cần tải về máy tính.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var dlg = new OpenFolderDialog
        {
            Title = $"Chọn thư mục lưu {items.Count} mục tải về từ FTP"
        };

        if (dlg.ShowDialog(this) != true) return;
        string targetDir = dlg.FolderName;

        var settings = ReadSettingsFromUi();
        SetFmBusy(true);
        try
        {
            _logger.Info($"[File Manager] Bắt đầu tải {items.Count} mục về '{targetDir}'...");
            using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(10));
            var (success, failed) = await _ftpService.DownloadItemsAsync(settings, items, targetDir, cts.Token);

            string msg = $"Đã hoàn tất tải về!\n- Thành công: {success} mục\n- Thất bại: {failed} mục\nĐường dẫn: {targetDir}";
            _logger.Info($"[File Manager] {msg}");
            MessageBox.Show(this, msg, "Kết quả Tải Về", MessageBoxButton.OK, failed > 0 ? MessageBoxImage.Warning : MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            _logger.Error($"[File Manager] Lỗi khi tải về: {ex.Message}");
            MessageBox.Show(this, $"Lỗi trong quá trình tải về:\n{ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            SetFmBusy(false);
        }
    }

    private void CtxFmDownload_Click(object sender, RoutedEventArgs e) => BtnFmDownload_Click(sender, e);

    private async void BtnFmUploadFile_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Title = $"Chọn các file tải lên FTP ('{_currentFmRemotePath}')",
            Multiselect = true
        };

        if (dlg.ShowDialog(this) != true || dlg.FileNames.Length == 0) return;

        var uploadEntries = dlg.FileNames.Select(f => new FileManagerEntry
        {
            Name = Path.GetFileName(f),
            FullPath = f,
            IsDirectory = false,
            Size = new FileInfo(f).Length
        }).ToList();

        var settings = ReadSettingsFromUi();
        SetFmBusy(true);
        try
        {
            _logger.Info($"[File Manager] Bắt đầu upload {uploadEntries.Count} file lên '{_currentFmRemotePath}'...");
            using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(15));
            var (success, failed) = await _ftpService.UploadItemsAsync(settings, uploadEntries, _currentFmRemotePath, cts.Token);

            string msg = $"Hoàn tất tải lên file!\n- Thành công: {success} file\n- Thất bại: {failed} file";
            _logger.Info($"[File Manager] {msg}");
            MessageBox.Show(this, msg, "Kết quả Tải Lên", MessageBoxButton.OK, failed > 0 ? MessageBoxImage.Warning : MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            _logger.Error($"[File Manager] Lỗi khi upload: {ex.Message}");
            MessageBox.Show(this, $"Lỗi trong quá trình tải lên:\n{ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            SetFmBusy(false);
            await LoadFileManagerDirectoryAsync(_currentFmRemotePath);
        }
    }

    private void CtxFmUploadFile_Click(object sender, RoutedEventArgs e) => BtnFmUploadFile_Click(sender, e);

    private async void BtnFmUploadFolder_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFolderDialog
        {
            Title = $"Chọn thư mục cần tải lên FTP ('{_currentFmRemotePath}')"
        };

        if (dlg.ShowDialog(this) != true || string.IsNullOrWhiteSpace(dlg.FolderName)) return;

        string folderPath = dlg.FolderName;
        string folderName = Path.GetFileName(folderPath);
        if (string.IsNullOrEmpty(folderName))
        {
            folderName = new DirectoryInfo(folderPath).Name;
        }

        var uploadEntries = new List<FileManagerEntry>
        {
            new FileManagerEntry
            {
                Name = folderName,
                FullPath = folderPath,
                IsDirectory = true
            }
        };

        var settings = ReadSettingsFromUi();
        SetFmBusy(true);
        try
        {
            _logger.Info($"[File Manager] Bắt đầu upload thư mục '{folderPath}' lên '{_currentFmRemotePath}'...");
            using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(30));
            var (success, failed) = await _ftpService.UploadItemsAsync(settings, uploadEntries, _currentFmRemotePath, cts.Token);

            string msg = $"Hoàn tất tải lên thư mục!\n- Thành công: {success} thư mục\n- Thất bại: {failed} thư mục";
            _logger.Info($"[File Manager] {msg}");
            MessageBox.Show(this, msg, "Kết quả Tải Lên", MessageBoxButton.OK, failed > 0 ? MessageBoxImage.Warning : MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            _logger.Error($"[File Manager] Lỗi khi upload thư mục: {ex.Message}");
            MessageBox.Show(this, $"Lỗi trong quá trình tải lên thư mục:\n{ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            SetFmBusy(false);
            await LoadFileManagerDirectoryAsync(_currentFmRemotePath);
        }
    }

    private void CtxFmUploadFolder_Click(object sender, RoutedEventArgs e) => BtnFmUploadFolder_Click(sender, e);

    private async void BtnFmNewFolder_Click(object sender, RoutedEventArgs e)
    {
        string? folderName = PromptDialogWindow.Show(
            this,
            "Tạo Thư Mục Mới",
            $"Nhập tên thư mục muốn tạo tại '{_currentFmRemotePath}':",
            "ThuMucMoi",
            PackIconKind.FolderPlusOutline);

        if (string.IsNullOrWhiteSpace(folderName)) return;

        var settings = ReadSettingsFromUi();
        string newPath = FtpCleanupService.CombineRemotePath(_currentFmRemotePath, folderName.Trim());

        SetFmBusy(true);
        try
        {
            _logger.Info($"[File Manager] Đang tạo thư mục: '{newPath}'...");
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            await _ftpService.CreateRemoteDirectoryAsync(settings, newPath, cts.Token);
            _logger.Info($"[File Manager] Tạo thư mục thành công: '{newPath}'");
        }
        catch (Exception ex)
        {
            _logger.Error($"[File Manager] Lỗi khi tạo thư mục '{newPath}': {ex.Message}");
            MessageBox.Show(this, $"Không thể tạo thư mục:\n{ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            SetFmBusy(false);
            await LoadFileManagerDirectoryAsync(_currentFmRemotePath);
        }
    }

    private void CtxFmNewFolder_Click(object sender, RoutedEventArgs e) => BtnFmNewFolder_Click(sender, e);

    private async void BtnFmDelete_Click(object sender, RoutedEventArgs e)
    {
        var items = GetSelectedOrActiveEntries();
        if (items.Count == 0)
        {
            MessageBox.Show(this, "Vui lòng chọn ít nhất một file hoặc thư mục để xóa khỏi FTP.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        string desc = items.Count == 1 ? $"'{items[0].Name}'" : $"{items.Count} mục";
        var confirm = MessageBox.Show(
            this,
            $"Bạn có chắc chắn muốn XÓA VĨNH VIỄN {desc} trên FTP Server không?\r\n\r\nĐường dẫn: {_currentFmRemotePath}\r\nCẢNH BÁO: Thao tác này không thể hoàn tác!",
            "Xác nhận xóa trên FTP",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (confirm != MessageBoxResult.Yes) return;

        var settings = ReadSettingsFromUi();
        SetFmBusy(true);
        try
        {
            _logger.Info($"[File Manager] Bắt đầu xóa {items.Count} mục trên FTP...");
            using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(10));
            var (deleted, failed) = await _ftpService.DeleteRemoteEntriesAsync(settings, items, cts.Token);

            string msg = $"Hoàn tất xóa!\n- Đã xóa: {deleted} mục\n- Lỗi: {failed} mục";
            _logger.Info($"[File Manager] {msg}");
            MessageBox.Show(this, msg, "Kết quả Xóa", MessageBoxButton.OK, failed > 0 ? MessageBoxImage.Warning : MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            _logger.Error($"[File Manager] Lỗi khi xóa trên FTP: {ex.Message}");
            MessageBox.Show(this, $"Lỗi trong quá trình xóa:\n{ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            SetFmBusy(false);
            await LoadFileManagerDirectoryAsync(_currentFmRemotePath);
        }
    }

    private void CtxFmDelete_Click(object sender, RoutedEventArgs e) => BtnFmDelete_Click(sender, e);

    private void CtxFmRefresh_Click(object sender, RoutedEventArgs e) => BtnFmRefresh_Click(sender, e);

    private void BtnClearLog_Click(object sender, RoutedEventArgs e)
    {
        TxtLogs.Clear();
    }
}
