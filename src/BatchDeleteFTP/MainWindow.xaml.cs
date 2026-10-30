using System;
using System.Collections.ObjectModel;
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
    private readonly ObservableCollection<FtpFileCandidate> _candidates = new();
    private FtpSettings _currentSettings = new();
    private bool _suppressProfileSelectionChanged;
    private bool _suppressThemeChanged;

    public MainWindow() : this(new ConfigService(), new AppLogger())
    {
    }

    private MainWindow(ConfigService configService, AppLogger logger)
        : this(configService, logger, new FtpCleanupService(logger))
    {
    }

    public MainWindow(ConfigService configService, AppLogger logger, FtpCleanupService ftpService)
    {
        InitializeComponent();
        _configService = configService;
        _logger = logger;
        _ftpService = ftpService;

        GridCandidates.ItemsSource = _candidates;

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
        }
        catch (Exception ex)
        {
            _logger.Error($"Lỗi khi Xem Trước: {ex.Message}");
            MessageBox.Show($"Lỗi khi quét file trên FTP:\n{ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
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

    private void BtnClearLog_Click(object sender, RoutedEventArgs e)
    {
        TxtLogs.Clear();
    }
}
