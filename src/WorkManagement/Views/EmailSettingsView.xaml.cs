using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using MaterialDesignThemes.Wpf;
using WorkManagement.Models;
using WorkManagement.Services;

namespace WorkManagement.Views;

public partial class EmailSettingsView : UserControl
{
    private FtpSettings _settings = new();
    private EmailNotificationService _emailService = null!;
    private Func<IReadOnlyList<FtpFileCandidate>>? _candidatesProvider;
    private bool _isSmtpPasswordRevealed;

    public event EventHandler? SettingsSaved;

    public EmailSettingsView()
    {
        InitializeComponent();
    }

    public void Initialize(
        FtpSettings settings,
        EmailNotificationService emailService,
        Func<IReadOnlyList<FtpFileCandidate>>? candidatesProvider = null)
    {
        _settings = settings;
        _emailService = emailService;
        _candidatesProvider = candidatesProvider;

        LoadFromSettings();
    }

    public void LoadFromSettings()
    {
        if (_settings == null) return;
        TxtSmtpHost.Text = string.IsNullOrWhiteSpace(_settings.SmtpHost) ? "mail.fujinet.net" : _settings.SmtpHost;
        int port = _settings.SmtpPort is <= 0 or 143 ? 25 : _settings.SmtpPort;
        TxtSmtpPort.Text = port.ToString();
        ChkSmtpSsl.IsChecked = _settings.SmtpEnableSsl;
        TxtSmtpUser.Text = _settings.SmtpUser;
        TxtSmtpPassword.Password = _settings.SmtpPassword;
        TxtSmtpPasswordRevealed.Text = _settings.SmtpPassword;
        TxtSenderEmail.Text = _settings.SenderEmail;
        TxtRecipientEmails.Text = _settings.RecipientEmails != null
            ? string.Join("; ", _settings.RecipientEmails)
            : string.Empty;
        ChkAutoSendOnPreview.IsChecked = _settings.AutoSendEmailOnPreview;
        UpdateRecipientCount();
    }

    public void ApplyToSettings(FtpSettings target)
    {
        int.TryParse(TxtSmtpPort.Text, out int smtpPort);
        if (smtpPort <= 0) smtpPort = 25;

        string smtpPass = _isSmtpPasswordRevealed ? TxtSmtpPasswordRevealed.Text : TxtSmtpPassword.Password;

        target.SmtpHost = TxtSmtpHost.Text.Trim();
        target.SmtpPort = smtpPort;
        target.SmtpEnableSsl = ChkSmtpSsl.IsChecked == true;
        target.SmtpUser = TxtSmtpUser.Text.Trim();
        target.SmtpPassword = smtpPass;
        target.SenderEmail = TxtSenderEmail.Text.Trim();
        target.RecipientEmails = EmailNotificationService.ParseRecipientEmails(TxtRecipientEmails.Text);
        target.AutoSendEmailOnPreview = ChkAutoSendOnPreview.IsChecked == true;
        ConfigService.SyncEmailSettingsAcrossAllProfiles(target);
    }

    private void BtnToggleSmtpPassword_Click(object sender, RoutedEventArgs e)
    {
        _isSmtpPasswordRevealed = !_isSmtpPasswordRevealed;
        if (_isSmtpPasswordRevealed)
        {
            TxtSmtpPasswordRevealed.Text = TxtSmtpPassword.Password;
            TxtSmtpPassword.Visibility = Visibility.Collapsed;
            TxtSmtpPasswordRevealed.Visibility = Visibility.Visible;
            IconSmtpPassEye.Kind = PackIconKind.EyeOffOutline;
        }
        else
        {
            TxtSmtpPassword.Password = TxtSmtpPasswordRevealed.Text;
            TxtSmtpPasswordRevealed.Visibility = Visibility.Collapsed;
            TxtSmtpPassword.Visibility = Visibility.Visible;
            IconSmtpPassEye.Kind = PackIconKind.EyeOutline;
        }
    }

    private void TxtRecipientEmails_TextChanged(object sender, TextChangedEventArgs e)
    {
        UpdateRecipientCount();
    }

    private void UpdateRecipientCount()
    {
        if (TxtRecipientCount == null) return;
        var parsed = EmailNotificationService.ParseRecipientEmails(TxtRecipientEmails.Text);
        TxtRecipientCount.Text = $"{parsed.Count} email hợp lệ";
    }

    private async void BtnTestEmail_Click(object sender, RoutedEventArgs e)
    {
        var parsed = EmailNotificationService.ParseRecipientEmails(TxtRecipientEmails.Text);
        if (parsed.Count == 0)
        {
            MessageBox.Show(Window.GetWindow(this), "Vui lòng nhập ít nhất 1 địa chỉ Email người nhận hợp lệ.", "Thiếu người nhận", MessageBoxButton.OK, MessageBoxImage.Warning);
            TxtRecipientEmails.Focus();
            return;
        }

        ApplyToSettings(_settings);

        BtnTestEmail.IsEnabled = false;
        BtnSaveEmailSettings.IsEnabled = false;
        Mouse.OverrideCursor = Cursors.Wait;
        TxtTestEmailLabel.Text = "Đang gửi...";

        try
        {
            var candidates = _candidatesProvider?.Invoke() ?? new List<FtpFileCandidate>();
            await _emailService.SendPreviewReportAsync(_settings, candidates);
            MessageBox.Show(
                Window.GetWindow(this),
                $"Đã gửi email báo cáo thử nghiệm thành công tới {parsed.Count} địa chỉ:\n{string.Join("\n", parsed)}",
                "Gửi Email Thành Công",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(Window.GetWindow(this), $"Gửi email thất bại:\n{ex.Message}", "Lỗi Gửi Email", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            Mouse.OverrideCursor = null;
            TxtTestEmailLabel.Text = "Gửi Email Thử (Test)";
            BtnTestEmail.IsEnabled = true;
            BtnSaveEmailSettings.IsEnabled = true;
        }
    }

    private void BtnSaveEmailSettings_Click(object sender, RoutedEventArgs e)
    {
        var parsed = EmailNotificationService.ParseRecipientEmails(TxtRecipientEmails.Text);
        if (!string.IsNullOrWhiteSpace(TxtRecipientEmails.Text) && parsed.Count == 0)
        {
            MessageBox.Show(Window.GetWindow(this), "Địa chỉ email người nhận không đúng định dạng. Vui lòng kiểm tra lại!", "Email không hợp lệ", MessageBoxButton.OK, MessageBoxImage.Warning);
            TxtRecipientEmails.Focus();
            return;
        }

        ApplyToSettings(_settings);
        SettingsSaved?.Invoke(this, EventArgs.Empty);
        MessageBox.Show(Window.GetWindow(this), "Đã lưu cấu hình Email thành công!", "Thành công", MessageBoxButton.OK, MessageBoxImage.Information);
    }
}
