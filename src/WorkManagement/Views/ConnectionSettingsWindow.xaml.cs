using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Win32;
using WorkManagement.Models;
using WorkManagement.Services;

namespace WorkManagement.Views;

public partial class ConnectionSettingsWindow : Window
{
    private readonly FtpSettings _settings;
    private readonly ConfigService _configService;
    private readonly FtpCleanupService _ftpService;
    private readonly AppLogger _logger;
    private bool _suppressProfileSelectionChanged;

    public ConnectionSettingsWindow(
        FtpSettings settings,
        ConfigService configService,
        FtpCleanupService ftpService,
        AppLogger logger)
    {
        InitializeComponent();
        _settings = settings;
        _configService = configService;
        _ftpService = ftpService;
        _logger = logger;

        RefreshProfileComboBox(_settings.SelectedProfileName);
        LoadSettingsToUi(_settings);
    }

    private void RefreshProfileComboBox(string? selectProfileName = null)
    {
        _suppressProfileSelectionChanged = true;
        try
        {
            _configService.EnsureProfilesInitialized(_settings);
            CboProfiles.ItemsSource = null;
            CboProfiles.DisplayMemberPath = nameof(FtpSiteProfile.Name);
            CboProfiles.SelectedValuePath = nameof(FtpSiteProfile.Name);
            CboProfiles.ItemsSource = _settings.Profiles;

            string selectedProfileName = selectProfileName ?? _settings.SelectedProfileName;
            var matched = _settings.Profiles.FirstOrDefault(p =>
                string.Equals(p.Name, selectedProfileName, StringComparison.OrdinalIgnoreCase))
                ?? _settings.Profiles.FirstOrDefault();

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
        TxtRetentionDays.Text = s.RetentionDays.ToString();

        ChkUseProxy.IsChecked = s.UseProxy;
        TxtProxyHost.Text = s.ProxyHost;
        TxtProxyPort.Text = s.ProxyPort.ToString();
        TxtProxyUser.Text = s.ProxyUser;
        TxtProxyPassword.Password = s.ProxyPassword;

        TxtRemotePaths.Text = string.Join(";", s.RemotePaths ?? new List<string> { "/" });
        ChkEnableBackup.IsChecked = s.EnableBackup;
        TxtLocalBackupPath.Text = s.LocalBackupPath;
    }

    public FtpSettings ReadSettingsFromUi()
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
            ? _settings.SelectedProfileName
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
            IsDarkTheme = _settings.IsDarkTheme,
            SmtpHost = _settings.SmtpHost,
            SmtpPort = _settings.SmtpPort,
            SmtpEnableSsl = _settings.SmtpEnableSsl,
            SmtpUser = _settings.SmtpUser,
            SmtpPassword = _settings.SmtpPassword,
            SenderEmail = _settings.SenderEmail,
            RecipientEmails = new List<string>(_settings.RecipientEmails ?? new List<string>()),
            AutoSendEmailOnPreview = _settings.AutoSendEmailOnPreview,
            Profiles = _settings.Profiles
        };
    }

    private void ApplyToSettings(FtpSettings target)
    {
        var read = ReadSettingsFromUi();
        target.SelectedProfileName = read.SelectedProfileName;
        target.Host = read.Host;
        target.Port = read.Port;
        target.EncryptionMode = read.EncryptionMode;
        target.Username = read.Username;
        target.Password = read.Password;
        target.UseProxy = read.UseProxy;
        target.ProxyHost = read.ProxyHost;
        target.ProxyPort = read.ProxyPort;
        target.ProxyUser = read.ProxyUser;
        target.ProxyPassword = read.ProxyPassword;
        target.RemotePaths = read.RemotePaths;
        target.RetentionDays = read.RetentionDays;
        target.EnableBackup = read.EnableBackup;
        target.LocalBackupPath = read.LocalBackupPath;
    }

    private void CboProfiles_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressProfileSelectionChanged) return;
        if (CboProfiles.SelectedItem is FtpSiteProfile selectedProfile)
        {
            selectedProfile.ApplyTo(_settings);
            LoadSettingsToUi(_settings);
            _logger.Info($"Đã chuyển sang Profile: '{selectedProfile.Name}' ({selectedProfile.Username}@{selectedProfile.Host}:{selectedProfile.Port}).");
        }
    }

    private void BtnNewProfile_Click(object sender, RoutedEventArgs e)
    {
        int index = _settings.Profiles.Count + 1;
        string newName = $"Site_{index}";
        while (_settings.Profiles.Any(p => string.Equals(p.Name, newName, StringComparison.OrdinalIgnoreCase)))
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
        _logger.Info($"Đang tạo Profile mới '{newName}'. Hãy nhập thông tin và nhấn 'Lưu Profile'.");
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

        ApplyToSettings(_settings);
        _configService.UpsertProfile(_settings, profileName);
        _configService.Save(_settings);

        RefreshProfileComboBox(profileName);
        TxtRemotePaths.Text = string.Join(";", _settings.RemotePaths);
        _logger.Info($"Đã lưu Profile '{profileName}' vào cấu hình.");
        MessageBox.Show($"Đã lưu Profile '{profileName}' thành công!", "Thành công", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void BtnDeleteProfile_Click(object sender, RoutedEventArgs e)
    {
        string targetName = (CboProfiles.SelectedItem as FtpSiteProfile)?.Name ?? CboProfiles.Text.Trim();
        if (string.IsNullOrWhiteSpace(targetName)) return;

        var confirm = MessageBox.Show(
            $"Bạn có chắc chắn muốn xóa Profile '{targetName}' không?",
            "Xác nhận xóa Profile",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (confirm != MessageBoxResult.Yes) return;

        if (_configService.DeleteProfile(_settings, targetName))
        {
            _configService.Save(_settings);
            RefreshProfileComboBox(_settings.SelectedProfileName);
            LoadSettingsToUi(_settings);
            _logger.Info($"Đã xóa Profile '{targetName}'. Đang chọn: '{_settings.SelectedProfileName}'.");
        }
    }

    private void BtnBrowseRemote_Click(object sender, RoutedEventArgs e)
    {
        var tempSettings = ReadSettingsFromUi();
        var picker = new RemoteFolderPickerWindow(tempSettings, _ftpService, tempSettings.RemotePaths)
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

    private async void BtnTestConnection_Click(object sender, RoutedEventArgs e)
    {
        SetBusy(true, "Đang kết nối...");
        try
        {
            var tempSettings = ReadSettingsFromUi();
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            await Task.Run(() => _ftpService.TestConnectionAsync(tempSettings, cts.Token));
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
            SetBusy(false);
        }
    }

    private void SetBusy(bool isBusy, string? text = null)
    {
        BtnTestConnection.IsEnabled = !isBusy;
        BtnSaveAndApply.IsEnabled = !isBusy;
        BtnSaveProfile.IsEnabled = !isBusy;
        BtnNewProfile.IsEnabled = !isBusy;
        BtnDeleteProfile.IsEnabled = !isBusy;
        BusyBar.Visibility = isBusy ? Visibility.Visible : Visibility.Collapsed;

        if (isBusy)
        {
            Mouse.OverrideCursor = Cursors.Wait;
            TxtTestConnectionLabel.Text = text ?? "Đang xử lý...";
        }
        else
        {
            Mouse.OverrideCursor = null;
            TxtTestConnectionLabel.Text = "Kiểm Tra Kết Nối";
        }
    }

    private void BtnSaveAndApply_Click(object sender, RoutedEventArgs e)
    {
        ApplyToSettings(_settings);
        _configService.Save(_settings);
        _logger.Info($"Đã lưu cấu hình (Profile '{_settings.SelectedProfileName}') thành công.");
        DialogResult = true;
        Close();
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
