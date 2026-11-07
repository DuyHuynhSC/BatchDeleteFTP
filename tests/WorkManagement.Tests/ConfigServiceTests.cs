using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Windows;
using WorkManagement.Models;
using WorkManagement.Services;
using MaterialDesignThemes.Wpf;
using Xunit;

namespace WorkManagement.Tests;

[Collection("WpfApp")]
public class ConfigServiceTests
{
    [Fact]
    public void SaveAndLoad_PreservesSettingsAndRemotePaths()
    {
        string tempFile = Path.Combine(Path.GetTempPath(), $"ftp_config_{Guid.NewGuid()}.json");
        try
        {
            var service = new ConfigService();
            var original = new FtpSettings
            {
                Host = "ftp.xxx.net",
                Port = 6021,
                Username = "XXX-USER",
                Password = "SecretPassword123",
                UseProxy = true,
                ProxyHost = "proxy.xxx.vn",
                ProxyPort = 8080,
                ProxyUser = "domain\\user",
                ProxyPassword = "proxypassword",
                RemotePaths = new List<string> { "/FolderA", "/FolderB/Sub" },
                RetentionDays = 1,
                EnableBackup = true,
                LocalBackupPath = @"C:\Temp\FTPBackup",
                IsDarkTheme = true
            };

            service.Save(original, tempFile);
            FtpSettings loaded = service.Load(tempFile);

            Assert.Equal("ftp.xxx.net", loaded.Host);
            Assert.Equal(6021, loaded.Port);
            Assert.Equal("XXX-USER", loaded.Username);
            Assert.Equal("SecretPassword123", loaded.Password);
            Assert.True(loaded.UseProxy);
            Assert.Equal("proxy.xxx.vn", loaded.ProxyHost);
            Assert.Equal(8080, loaded.ProxyPort);
            Assert.Equal("domain\\user", loaded.ProxyUser);
            Assert.Equal("proxypassword", loaded.ProxyPassword);
            Assert.Equal(new[] { "/FolderA", "/FolderB/Sub" }, loaded.RemotePaths);
            Assert.True(loaded.EnableBackup);
            Assert.Equal(@"C:\Temp\FTPBackup", loaded.LocalBackupPath);
            Assert.True(loaded.IsDarkTheme);
            Assert.NotEmpty(loaded.Profiles);
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    [Fact]
    public void UpsertSelectAndDeleteProfiles_ManagesMultipleSiteProfiles()
    {
        string tempFile = Path.Combine(Path.GetTempPath(), $"ftp_profiles_{Guid.NewGuid()}.json");
        try
        {
            var service = new ConfigService();
            var settings = new FtpSettings
            {
                SelectedProfileName = "SITE_A",
                Host = "ftp.xxx.net",
                Port = 6021,
                Username = "USER-A",
                Password = "pass1"
            };
            service.Save(settings, tempFile);

            // Add second profile
            settings.Host = "ftp2.xxx.net";
            settings.Port = 21;
            settings.EncryptionMode = "Explicit";
            settings.Username = "USER-B";
            settings.Password = "pass2";
            service.UpsertProfile(settings, "SITE_B");
            service.Save(settings, tempFile);

            var reloaded = service.Load(tempFile);
            Assert.Equal(2, reloaded.Profiles.Count);
            Assert.Equal("SITE_B", reloaded.SelectedProfileName);
            Assert.Equal("USER-B", reloaded.Username);

            // Switch back to SITE_A
            bool switched = service.SelectProfile(reloaded, "SITE_A");
            Assert.True(switched);
            Assert.Equal("SITE_A", reloaded.SelectedProfileName);
            Assert.Equal("ftp.xxx.net", reloaded.Host);
            Assert.Equal(6021, reloaded.Port);
            Assert.Equal("USER-A", reloaded.Username);

            // Delete SITE_A -> falls back to SITE_B
            bool deleted = service.DeleteProfile(reloaded, "SITE_A");
            Assert.True(deleted);
            Assert.Single(reloaded.Profiles);
            Assert.Equal("SITE_B", reloaded.SelectedProfileName);
            Assert.Equal("USER-B", reloaded.Username);
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    [Fact]
    public void MainWindow_InitializesMaterialThemeAndTogglesDarkLightWithoutError()
    {
        Exception? threadEx = null;
        var thread = new Thread(() =>
        {
            try
            {
                if (Application.Current == null)
                {
                    var app = new App();
                    app.InitializeComponent();
                }

                var window = new MainWindow();
                var paletteHelper = new PaletteHelper();

                window.SetDarkTheme(true);
                Assert.Equal(BaseTheme.Dark, paletteHelper.GetTheme().GetBaseTheme());
                var darkBg = (window.Background as System.Windows.Media.SolidColorBrush)?.Color;

                window.SetDarkTheme(false);
                Assert.Equal(BaseTheme.Light, paletteHelper.GetTheme().GetBaseTheme());
                var lightBg = (window.Background as System.Windows.Media.SolidColorBrush)?.Color;

                Assert.NotNull(darkBg);
                Assert.NotNull(lightBg);
                Assert.NotEqual(darkBg, lightBg);
                Assert.NotNull(Application.Current!.TryFindResource("PrimaryHueMidBrush"));

                var picker = new WorkManagement.Views.RemoteFolderPickerWindow(
                    new FtpSettings(),
                    new FtpCleanupService(new AppLogger()),
                    new[] { "/" });
                Assert.NotNull(picker.Background);

                // Test: Khi tick chọn thư mục con, thư mục gốc ("/") phải tự động bị bỏ chọn
                var root = picker.RootNodes[0];
                Assert.True(root.IsSelected);
                var child = new FtpFolderNode { Name = "FolderA", FullPath = "/FolderA" };
                root.Children.Add(child);
                child.IsSelected = true;
                Assert.False(root.IsSelected);
                Assert.True(child.IsSelected);

                var emailSettings = new FtpSettings
                {
                    SmtpHost = "smtp.office365.com",
                    SmtpPort = 587,
                    RecipientEmails = new List<string> { "mem1@corp.com", "mem2@corp.com" }
                };
                var emailDialog = new WorkManagement.Views.EmailSettingsWindow(
                    emailSettings,
                    new EmailNotificationService(new AppLogger()),
                    Array.Empty<FtpFileCandidate>());
                var updatedEmail = new FtpSettings();
                emailDialog.ApplyToSettings(updatedEmail);
                Assert.Equal("smtp.office365.com", updatedEmail.SmtpHost);
                Assert.Equal(587, updatedEmail.SmtpPort);
                Assert.Equal(new[] { "mem1@corp.com", "mem2@corp.com" }, updatedEmail.RecipientEmails);

                var connSettings = new FtpSettings
                {
                    Host = "ftp.custom-test.net",
                    Port = 2121,
                    Username = "test-user",
                    RetentionDays = 5
                };
                var connDialog = new WorkManagement.Views.ConnectionSettingsWindow(
                    connSettings,
                    new ConfigService(),
                    new FtpCleanupService(new AppLogger()),
                    new AppLogger());
                var readConn = connDialog.ReadSettingsFromUi();
                Assert.Equal("ftp.custom-test.net", readConn.Host);
                Assert.Equal(2121, readConn.Port);
                Assert.Equal("test-user", readConn.Username);
                Assert.Equal(5, readConn.RetentionDays);

                // Verify SystemTrayService ContextMenu construction & CliOptions --tray parsing
                Assert.NotNull(window.TrayService);
                var trayMenu = window.TrayService!.BuildContextMenu();
                Assert.NotNull(trayMenu);
                Assert.True(trayMenu.Items.Count >= 6);

                var cliTray = WorkManagement.Helpers.CliOptions.Parse(new[] { "--tray" });
                Assert.True(cliTray.StartMinimizedToTray);
                Assert.False(cliTray.IsConsoleMode);
            }
            catch (Exception ex)
            {
                threadEx = ex;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        Assert.Null(threadEx);
    }
    [Fact]
    public void EmailSettings_AreGlobalAndAppliedAcrossAllProfiles()
    {
        string tempFile = Path.Combine(Path.GetTempPath(), $"ftp_email_global_{Guid.NewGuid()}.json");
        try
        {
            var service = new ConfigService();
            var settings = new FtpSettings
            {
                SelectedProfileName = "SITE_A",
                Host = "ftp-a.example.com",
                Username = "userA",
                SmtpHost = "smtp.corp.com",
                SmtpPort = 587,
                SmtpEnableSsl = true,
                SmtpUser = "notification@corp.com",
                SenderEmail = "no-reply@corp.com",
                RecipientEmails = new List<string> { "team@corp.com", "admin@corp.com" },
                AutoSendEmailOnPreview = true
            };

            service.Save(settings, tempFile);

            // Add second profile SITE_B
            settings.Host = "ftp-b.example.com";
            settings.Username = "userB";
            service.UpsertProfile(settings, "SITE_B");
            service.Save(settings, tempFile);

            var loaded = service.Load(tempFile);
            Assert.Equal(2, loaded.Profiles.Count);

            foreach (var prof in loaded.Profiles)
            {
                Assert.Equal("smtp.corp.com", prof.SmtpHost);
                Assert.Equal(587, prof.SmtpPort);
                Assert.True(prof.SmtpEnableSsl);
                Assert.Equal(new[] { "team@corp.com", "admin@corp.com" }, prof.RecipientEmails);
                Assert.True(prof.AutoSendEmailOnPreview);
            }

            // Switch to SITE_A -> global email settings preserved
            bool switched = service.SelectProfile(loaded, "SITE_A");
            Assert.True(switched);
            Assert.Equal("SITE_A", loaded.SelectedProfileName);
            Assert.Equal("smtp.corp.com", loaded.SmtpHost);
            Assert.Equal(new[] { "team@corp.com", "admin@corp.com" }, loaded.RecipientEmails);

            // Update email settings globally and save
            loaded.SmtpHost = "mail.new-domain.com";
            loaded.RecipientEmails = new List<string> { "updated@corp.com" };
            service.Save(loaded, tempFile);

            var reloaded = service.Load(tempFile);
            Assert.Equal("mail.new-domain.com", reloaded.SmtpHost);
            Assert.Equal(new[] { "updated@corp.com" }, reloaded.RecipientEmails);

            var profB = System.Linq.Enumerable.First(reloaded.Profiles, p => p.Name == "SITE_B");
            Assert.Equal("mail.new-domain.com", profB.SmtpHost);
            Assert.Equal(new[] { "updated@corp.com" }, profB.RecipientEmails);

            var converted = profB.ToSettings(reloaded);
            Assert.Equal("mail.new-domain.com", converted.SmtpHost);
            Assert.Equal(new[] { "updated@corp.com" }, converted.RecipientEmails);
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }
}