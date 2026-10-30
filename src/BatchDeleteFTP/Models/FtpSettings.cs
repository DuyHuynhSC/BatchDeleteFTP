using System.Collections.Generic;
using System.IO;

namespace BatchDeleteFTP.Models;

public class FtpSettings
{
    public string SelectedProfileName { get; set; } = "Default";
    public string Host { get; set; } = "ftp.xxx.net";
    public int Port { get; set; } = 6021;
    public string EncryptionMode { get; set; } = "Implicit";
    public string Username { get; set; } = "XXX-USER";
    public string Password { get; set; } = string.Empty;
    public bool UseProxy { get; set; } = true;
    public string ProxyHost { get; set; } = "proxy.xxx.vn";
    public int ProxyPort { get; set; } = 8080;
    public string ProxyUser { get; set; } = string.Empty;
    public string ProxyPassword { get; set; } = string.Empty;
    public List<string> RemotePaths { get; set; } = new() { "/" };
    public int RetentionDays { get; set; } = 1;
    public bool EnableBackup { get; set; } = true;
    public string LocalBackupPath { get; set; } = Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, "Backups");
    public bool IsDarkTheme { get; set; } = false;
    public List<FtpSiteProfile> Profiles { get; set; } = new();
}
