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
        RefreshProfileComboBox(_currentSettings.SelectedProfileName);
        LoadSettingsToUi(_currentSettings);
        ApplyTheme(_currentSettings.IsDarkTheme);
    }

    public void SetDarkTheme(bool isDark) => TglDarkTheme.IsChecked = isDark;

    private void ApplyTheme(bool isDark)
    {
        _suppressThemeChanged = true;
        try
        {
            TglDarkTheme.IsChecked = isDark;
            IconThemeMode.Kind = isDark ? PackIconKind.WeatherNight : PackIconKind.WeatherSunny;
            TxtThemeMode.Text = isDark ? "Dark" : "Light";

            var paletteHelper = new PaletteHelper();
            Theme theme = paletteHelper.GetTheme();
            theme.SetBaseTheme(isDark ? BaseTheme.Dark : BaseTheme.Light);
            theme.SetPrimaryColor(isDark
                ? System.Windows.Media.Color.FromRgb(0x42, 0xA5, 0xF5)
                : System.Windows.Media.Color.FromRgb(0x39, 0x49, 0xAB));
            paletteHelper.SetTheme(theme);

            HeaderBanner.Background = new System.Windows.Media.SolidColorBrush(isDark
                ? System.Windows.Media.Color.FromRgb(0x1E, 0x29, 0x3B)
                : System.Windows.Media.Color.FromRgb(0x28, 0x35, 0x93));

            if (Application.Current != null)
            {
                Application.Current.Resources["AppSectionIconBrush"] = new System.Windows.Media.SolidColorBrush(isDark
                    ? System.Windows.Media.Color.FromRgb(0x64, 0xB5, 0xF6)
                    : System.Windows.Media.Color.FromRgb(0x39, 0x49, 0xAB));
                Application.Current.Resources["AppOutlinedButtonForeground"] = new System.Windows.Media.SolidColorBrush(isDark
                    ? System.Windows.Media.Color.FromRgb(0xE3, 0xF2, 0xFD)
                    : System.Windows.Media.Color.FromRgb(0x1A, 0x23, 0x7E));
                Application.Current.Resources["AppOutlinedButtonBorder"] = new System.Windows.Media.SolidColorBrush(isDark
                    ? System.Windows.Media.Color.FromRgb(0x64, 0xB5, 0xF6)
                    : System.Windows.Media.Color.FromRgb(0x39, 0x49, 0xAB));
                Application.Current.Resources["AppOutlinedButtonBackground"] = new System.Windows.Media.SolidColorBrush(isDark
                    ? System.Windows.Media.Color.FromArgb(0x24, 0x42, 0xA5, 0xF5)
                    : System.Windows.Media.Color.FromArgb(0x12, 0x39, 0x49, 0xAB));
            }
        }
        finally
        {
            _suppressThemeChanged = false;
        }
    }

    private void TglDarkTheme_Changed(object sender, RoutedEventArgs e)
    {
        if (_suppressThemeChanged) return;

        bool isDark = TglDarkTheme.IsChecked == true;
        _currentSettings.IsDarkTheme = isDark;
        ApplyTheme(isDark);

        try
        {
            _configService.Save(_currentSettings);
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
        }
        finally
        {
            _suppressProfileSelectionChanged = false;
        }
    }

    private void LoadSettingsToUi(FtpSettings s)
    {
        TxtHost.Text = s.Host;
        TxtPort.Text = s.Port.ToString();
        CboEncryption.SelectedIndex = string.Equals(s.EncryptionMode, "Explicit", StringComparison.OrdinalIgnoreCase) ? 1 : 0;
        TxtUser.Text = s.Username;
        TxtPassword.Password = s.Password;
        ChkUseProxy.IsChecked = s.UseProxy;
        TxtProxyHost.Text = s.ProxyHost;
        TxtProxyPort.Text = s.ProxyPort.ToString();
        TxtProxyUser.Text = s.ProxyUser;
        TxtProxyPassword.Password = s.ProxyPassword;
        TxtRemotePaths.Text = string.Join(";", s.RemotePaths);
        TxtRetentionDays.Text = s.RetentionDays.ToString();
        ChkEnableBackup.IsChecked = s.EnableBackup;
        TxtLocalBackupPath.Text = s.LocalBackupPath;
    }

    private FtpSettings ReadSettingsFromUi()
    {
        int.TryParse(TxtPort.Text, out int port);
        if (port <= 0) port = 6021;

        int.TryParse(TxtProxyPort.Text, out int proxyPort);
        if (proxyPort <= 0) proxyPort = 8080;

        int.TryParse(TxtRetentionDays.Text, out int retentionDays);
        if (retentionDays < 0) retentionDays = 1;

        string encTag = (CboEncryption.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "Implicit";

        var paths = (TxtRemotePaths.Text ?? "/")
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();

        string profileName = string.IsNullOrWhiteSpace(CboProfiles.Text)
            ? _currentSettings.SelectedProfileName
            : CboProfiles.Text.Trim();

        return new FtpSettings
        {
            SelectedProfileName = profileName,
            Host = TxtHost.Text.Trim(),
            Port = port,
            EncryptionMode = encTag,
            Username = TxtUser.Text.Trim(),
            Password = TxtPassword.Password,
            UseProxy = ChkUseProxy.IsChecked == true,
            ProxyHost = TxtProxyHost.Text.Trim(),
            ProxyPort = proxyPort,
            ProxyUser = TxtProxyUser.Text.Trim(),
            ProxyPassword = TxtProxyPassword.Password,
            RemotePaths = FtpCleanupService.NormalizeRemotePaths(paths).ToList(),
            RetentionDays = retentionDays,
            EnableBackup = ChkEnableBackup.IsChecked == true,
            LocalBackupPath = TxtLocalBackupPath.Text.Trim(),
            IsDarkTheme = TglDarkTheme.IsChecked == true,
            SmtpHost = _currentSettings.SmtpHost,
            SmtpPort = _currentSettings.SmtpPort,
            SmtpEnableSsl = _currentSettings.SmtpEnableSsl,
            SmtpUser = _currentSettings.SmtpUser,
            SmtpPassword = _currentSettings.SmtpPassword,
            SenderEmail = _currentSettings.SenderEmail,
            RecipientEmails = new List<string>(_currentSettings.RecipientEmails ?? new List<string>()),
            AutoSendEmailOnPreview = _currentSettings.AutoSendEmailOnPreview,
            Profiles = _currentSettings.Profiles
        };
    }

    private void CboProfiles_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressProfileSelectionChanged) return;
        if (CboProfiles.SelectedItem is FtpSiteProfile selectedProfile)
        {
            selectedProfile.ApplyTo(_currentSettings);
            LoadSettingsToUi(_currentSettings);
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

        _currentSettings = ReadSettingsFromUi();
        _configService.UpsertProfile(_currentSettings, profileName);
        _configService.Save(_currentSettings);

        RefreshProfileComboBox(profileName);
        TxtRemotePaths.Text = string.Join(";", _currentSettings.RemotePaths);
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

    private void SetUiBusy(bool isBusy, string? busyText = null)
    {
        CboProfiles.IsEnabled = !isBusy;
        BtnNewProfile.IsEnabled = !isBusy;
        BtnSaveProfile.IsEnabled = !isBusy;
        BtnDeleteProfile.IsEnabled = !isBusy;
        BtnSaveConfig.IsEnabled = !isBusy;
        BtnTestConnection.IsEnabled = !isBusy;
        BtnPreview.IsEnabled = !isBusy;
        BtnSendPreviewEmail.IsEnabled = !isBusy;
        BtnEmailSettings.IsEnabled = !isBusy;
        BtnExecuteDelete.IsEnabled = !isBusy;
        BtnBrowseRemote.IsEnabled = !isBusy;
        BusyProgressBar.Visibility = isBusy ? Visibility.Visible : Visibility.Collapsed;

        if (isBusy)
        {
            Mouse.OverrideCursor = Cursors.Wait;
            if (!string.IsNullOrEmpty(busyText))
            {
                TxtTestConnectionLabel.Text = busyText;
            }
        }
        else
        {
            Mouse.OverrideCursor = null;
            TxtTestConnectionLabel.Text = "Kiểm Tra Kết Nối";
        }
    }

    private void BtnSaveConfig_Click(object sender, RoutedEventArgs e)
    {
        _currentSettings = ReadSettingsFromUi();
        _configService.Save(_currentSettings);
        RefreshProfileComboBox(_currentSettings.SelectedProfileName);
        TxtRemotePaths.Text = string.Join(";", _currentSettings.RemotePaths);
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

    private void BtnBrowseRemote_Click(object sender, RoutedEventArgs e)
    {
        var settings = ReadSettingsFromUi();
        var picker = new RemoteFolderPickerWindow(settings, _ftpService, settings.RemotePaths)
        {
            Owner = this
        };

        if (picker.ShowDialog() == true)
        {
            TxtRemotePaths.Text = string.Join(";", picker.SelectedPaths);
            _logger.Info($"Đã chọn Remote Paths: {TxtRemotePaths.Text}");
        }
    }

    private void BtnBrowseLocalBackup_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Chọn thư mục lưu file Backup trên máy tính"
        };

        if (dialog.ShowDialog(this) == true)
        {
            TxtLocalBackupPath.Text = dialog.FolderName;
        }
    }

    private async void BtnPreview_Click(object sender, RoutedEventArgs e)
    {
        SetUiBusy(true);
        try
        {
            var settings = ReadSettingsFromUi();
            _candidates.Clear();
            var list = await _ftpService.ScanCandidatesAsync(settings, DateTime.Now);
            foreach (var item in list)
            {
                _candidates.Add(item);
            }

            if (settings.AutoSendEmailOnPreview && settings.RecipientEmails.Count > 0)
            {
                await _emailService.SendPreviewReportAsync(settings, _candidates);
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
                    var list = await _ftpService.ScanCandidatesAsync(_currentSettings, DateTime.Now);
                    foreach (var item in list)
                    {
                        _candidates.Add(item);
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
        var settings = ReadSettingsFromUi();
        SetUiBusy(true);
        try
        {
            if (_candidates.Count == 0)
            {
                var scanned = await _ftpService.ScanCandidatesAsync(settings, DateTime.Now);
                foreach (var item in scanned)
                {
                    _candidates.Add(item);
                }
            }

            int selectedCount = _candidates.Count(c => c.IsSelected);
            if (selectedCount == 0)
            {
                MessageBox.Show("Không có file nào thỏa điều kiện hoặc được chọn để xóa.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            string actionDesc = settings.EnableBackup
                ? $"tải Backup về '{settings.LocalBackupPath}' và XÓA {selectedCount} file"
                : $"XÓA VĨNH VIỄN {selectedCount} file (KHÔNG Backup)";

            var confirm = MessageBox.Show(
                $"Bạn có chắc chắn muốn {actionDesc} trên FTP Server không?",
                "Xác nhận xóa file",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (confirm != MessageBoxResult.Yes) return;

            var (deleted, failed) = await _ftpService.ExecuteBackupAndDeleteAsync(settings, _candidates);
            MessageBox.Show($"Hoàn tất xử lý!\n- Đã xóa: {deleted} file\n- Lỗi: {failed} file", "Kết quả", MessageBoxButton.OK, MessageBoxImage.Information);
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
            string initialPath = "/";
            var paths = (TxtRemotePaths.Text ?? "")
                .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (paths.Length > 0 && !string.IsNullOrWhiteSpace(paths[0]))
            {
                initialPath = paths[0];
            }
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
