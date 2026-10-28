using System;
using System.Collections.Generic;
using System.IO;
using BatchDeleteFTP.Models;
using BatchDeleteFTP.Services;
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
                Host = "ftp.fujinet.net",
                Port = 6021,
                Username = "UCD-MOROZOFF",
                Password = "SecretPassword123",
                UseProxy = true,
                ProxyHost = "prox2.fujinet.vn",
                ProxyPort = 8080,
                ProxyUser = "domain\\user",
                ProxyPassword = "proxypassword",
                RemotePaths = new List<string> { "/FolderA", "/FolderB/Sub" },
                RetentionDays = 1,
                EnableBackup = true,
                LocalBackupPath = @"C:\Temp\FTPBackup"
            };

            service.Save(original, tempFile);
            FtpSettings loaded = service.Load(tempFile);

            Assert.Equal("ftp.fujinet.net", loaded.Host);
            Assert.Equal(6021, loaded.Port);
            Assert.Equal("UCD-MOROZOFF", loaded.Username);
            Assert.Equal("SecretPassword123", loaded.Password);
            Assert.True(loaded.UseProxy);
            Assert.Equal("prox2.fujinet.vn", loaded.ProxyHost);
            Assert.Equal(8080, loaded.ProxyPort);
            Assert.Equal("domain\\user", loaded.ProxyUser);
            Assert.Equal("proxypassword", loaded.ProxyPassword);
            Assert.Equal(new[] { "/FolderA", "/FolderB/Sub" }, loaded.RemotePaths);
            Assert.True(loaded.EnableBackup);
            Assert.Equal(@"C:\Temp\FTPBackup", loaded.LocalBackupPath);
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }
}
