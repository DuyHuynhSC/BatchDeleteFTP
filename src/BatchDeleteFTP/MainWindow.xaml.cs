using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using BatchDeleteFTP.Models;
using BatchDeleteFTP.Services;
using BatchDeleteFTP.Views;
using Microsoft.Win32;

namespace BatchDeleteFTP;

public partial class MainWindow : Window
{
    private readonly ConfigService _configService;
    private readonly AppLogger _logger;
    private readonly FtpCleanupService _ftpService;
    private readonly ObservableCollection<FtpFileCandidate> _candidates = new();

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

        LoadSettingsToUi(_configService.Load());
    }

    private void LoadSettingsToUi(FtpSettings s)
    {
        TxtHost.Text = s.Host;
        TxtPort.Text = s.Port.ToString();
        CboEncryption.SelectedIndex = string.Equals(s.EncryptionMode, "Explicit", StringComparison.OrdinalIgnoreCase) ? 1 : 0;
        TxtUser.Text = s.Username;
        TxtPassword.Password = s.Password;
        TxtRemotePaths.Text = string.Join(";", s.RemotePaths);
        TxtRetentionDays.Text = s.RetentionDays.ToString();
        ChkEnableBackup.IsChecked = s.EnableBackup;
        TxtLocalBackupPath.Text = s.LocalBackupPath;
    }

    private FtpSettings ReadSettingsFromUi()
    {
        int.TryParse(TxtPort.Text, out int port);
        if (port <= 0) port = 6021;

        int.TryParse(TxtRetentionDays.Text, out int retentionDays);
        if (retentionDays < 0) retentionDays = 1;

        string encTag = (CboEncryption.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "Implicit";

        var paths = (TxtRemotePaths.Text ?? "/")
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();

        return new FtpSettings
        {
            Host = TxtHost.Text.Trim(),
            Port = port,
            EncryptionMode = encTag,
            Username = TxtUser.Text.Trim(),
            Password = TxtPassword.Password,
            RemotePaths = FtpCleanupService.NormalizeRemotePaths(paths).ToList(),
            RetentionDays = retentionDays,
            EnableBackup = ChkEnableBackup.IsChecked == true,
            LocalBackupPath = TxtLocalBackupPath.Text.Trim()
        };
    }

    private void SetUiBusy(bool isBusy)
    {
        BtnSaveConfig.IsEnabled = !isBusy;
        BtnTestConnection.IsEnabled = !isBusy;
        BtnPreview.IsEnabled = !isBusy;
        BtnExecuteDelete.IsEnabled = !isBusy;
        BtnBrowseRemote.IsEnabled = !isBusy;
    }

    private void BtnSaveConfig_Click(object sender, RoutedEventArgs e)
    {
        var settings = ReadSettingsFromUi();
        _configService.Save(settings);
        TxtRemotePaths.Text = string.Join(";", settings.RemotePaths);
        _logger.Info($"Đã lưu cấu hình vào '{_configService.DefaultConfigPath}'.");
        MessageBox.Show("Đã lưu cấu hình thành công!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private async void BtnTestConnection_Click(object sender, RoutedEventArgs e)
    {
        SetUiBusy(true);
        try
        {
            var settings = ReadSettingsFromUi();
            await _ftpService.TestConnectionAsync(settings);
            MessageBox.Show("Kết nối tới FTP Server thành công!", "Thành công", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            _logger.Error($"Kết nối thất bại: {ex.Message}");
            MessageBox.Show($"Kết nối thất bại:\n{ex.Message}", "Lỗi Kết Nối", MessageBoxButton.OK, MessageBoxImage.Error);
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
}
