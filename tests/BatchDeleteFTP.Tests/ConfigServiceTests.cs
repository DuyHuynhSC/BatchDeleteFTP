using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Windows;
using BatchDeleteFTP.Models;
using BatchDeleteFTP.Services;
using MaterialDesignThemes.Wpf;
using Xunit;

namespace BatchDeleteFTP.Tests;

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

                var picker = new BatchDeleteFTP.Views.RemoteFolderPickerWindow(
                    new FtpSettings(),
                    new FtpCleanupService(new AppLogger()),
                    new[] { "/" });
                Assert.NotNull(picker.Background);

                var emailSettings = new FtpSettings
                {
                    SmtpHost = "smtp.office365.com",
                    SmtpPort = 587,
                    RecipientEmails = new List<string> { "mem1@corp.com", "mem2@corp.com" }
                };
                var emailDialog = new BatchDeleteFTP.Views.EmailSettingsWindow(
                    emailSettings,
                    new EmailNotificationService(new AppLogger()),
                    Array.Empty<FtpFileCandidate>());
                var updatedEmail = new FtpSettings();
                emailDialog.ApplyToSettings(updatedEmail);
                Assert.Equal("smtp.office365.com", updatedEmail.SmtpHost);
                Assert.Equal(587, updatedEmail.SmtpPort);
                Assert.Equal(new[] { "mem1@corp.com", "mem2@corp.com" }, updatedEmail.RecipientEmails);
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
}
