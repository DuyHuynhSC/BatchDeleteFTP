using System;
using System.Collections.Generic;
using System.Linq;
using BatchDeleteFTP.Models;
using BatchDeleteFTP.Services;
using Xunit;

namespace BatchDeleteFTP.Tests;

public class MultiProfileScanTests
{
    [Fact]
    public void FtpSiteProfile_ToSettings_ProducesAccurateFtpSettings()
    {
        var profile = new FtpSiteProfile
        {
            Name = "BackupServer_VN",
            Host = "192.168.1.100",
            Port = 2121,
            EncryptionMode = "Explicit",
            Username = "user_vn",
            Password = "pwd_vn",
            UseProxy = false,
            RemotePaths = new List<string> { "/var/log/app", "/data/backup" },
            RetentionDays = 3,
            EnableBackup = true,
            LocalBackupPath = @"D:\Backups\VN"
        };

        var baseSettings = new FtpSettings
        {
            IsDarkTheme = true
        };

        var settings = profile.ToSettings(baseSettings);

        Assert.Equal("BackupServer_VN", settings.SelectedProfileName);
        Assert.Equal("192.168.1.100", settings.Host);
        Assert.Equal(2121, settings.Port);
        Assert.Equal("Explicit", settings.EncryptionMode);
        Assert.Equal("user_vn", settings.Username);
        Assert.Equal("pwd_vn", settings.Password);
        Assert.False(settings.UseProxy);
        Assert.Equal(2, settings.RemotePaths.Count);
        Assert.Equal(3, settings.RetentionDays);
        Assert.True(settings.EnableBackup);
        Assert.Equal(@"D:\Backups\VN", settings.LocalBackupPath);
        Assert.True(settings.IsDarkTheme);
    }

    [Fact]
    public void EmailNotificationService_BuildPreviewHtmlBody_RendersMultiProfileColumnsAndSummary()
    {
        var service = new EmailNotificationService(new AppLogger());
        var baseSettings = new FtpSettings
        {
            SelectedProfileName = "Site_A"
        };

        var candidates = new List<FtpFileCandidate>
        {
            new()
            {
                ProfileName = "Site_A",
                FullPath = "/data/file_a.txt",
                FileName = "file_a.txt",
                Size = 1024,
                ModifiedDate = DateTime.Now.AddDays(-5),
                IsSelected = true
            },
            new()
            {
                ProfileName = "Site_B",
                FullPath = "/data/file_b.txt",
                FileName = "file_b.txt",
                Size = 2048,
                ModifiedDate = DateTime.Now.AddDays(-6),
                IsSelected = true
            }
        };

        string html = service.BuildPreviewHtmlBody(baseSettings, candidates, DateTime.Now);

        Assert.Contains("Phạm vi quét (Profiles):", html);
        Assert.Contains("Site_A, Site_B", html);
        Assert.Contains("2 Site Profiles", html);
        Assert.Contains("Site Profile", html);
        Assert.Contains("/data/file_a.txt", html);
        Assert.Contains("/data/file_b.txt", html);
    }

    [Fact]
    public void CandidateGrouping_ByProfileName_CorrectlyGroupsFilesForExecution()
    {
        var candidates = new List<FtpFileCandidate>
        {
            new() { ProfileName = "Server1", FullPath = "/a.log", IsSelected = true },
            new() { ProfileName = "Server2", FullPath = "/b.log", IsSelected = true },
            new() { ProfileName = "Server1", FullPath = "/c.log", IsSelected = false },
            new() { ProfileName = "Server1", FullPath = "/d.log", IsSelected = true },
            new() { ProfileName = "Server2", FullPath = "/e.log", IsSelected = true },
        };

        var selected = candidates.Where(c => c.IsSelected).ToList();
        var groups = selected.GroupBy(c => c.ProfileName).ToDictionary(g => g.Key, g => g.ToList());

        Assert.Equal(2, groups.Count);
        Assert.Equal(2, groups["Server1"].Count);
        Assert.Equal(2, groups["Server2"].Count);
        Assert.Contains(groups["Server1"], c => c.FullPath == "/a.log");
        Assert.Contains(groups["Server1"], c => c.FullPath == "/d.log");
        Assert.DoesNotContain(groups["Server1"], c => c.FullPath == "/c.log");
    }

    [Fact]
    public void ConfigService_EnsuresAndPersists_SelectedScanProfileNames()
    {
        var configService = new ConfigService();
        var settings = new FtpSettings
        {
            SelectedProfileName = "Site_1",
            Profiles = new List<FtpSiteProfile>
            {
                new() { Name = "Site_1" },
                new() { Name = "Site_2" },
                new() { Name = "Site_3" }
            },
            SelectedScanProfileNames = new List<string> { "Site_1", "Site_3", "NonExistentSite" }
        };

        configService.EnsureProfilesInitialized(settings);

        // NonExistentSite should be filtered out
        Assert.Equal(2, settings.SelectedScanProfileNames.Count);
        Assert.Contains("Site_1", settings.SelectedScanProfileNames);
        Assert.Contains("Site_3", settings.SelectedScanProfileNames);
        Assert.DoesNotContain("NonExistentSite", settings.SelectedScanProfileNames);

        // Delete Site_3
        configService.DeleteProfile(settings, "Site_3");
        Assert.Single(settings.SelectedScanProfileNames);
        Assert.Equal("Site_1", settings.SelectedScanProfileNames[0]);
    }
}
