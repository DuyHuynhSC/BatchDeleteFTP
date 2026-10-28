using System.Collections.Generic;
using System.IO;

namespace BatchDeleteFTP.Models;

public class FtpSettings
{
    public string Host { get; set; } = "ftp.fujinet.net";
    public int Port { get; set; } = 6021;
    public string EncryptionMode { get; set; } = "Implicit";
    public string Username { get; set; } = "UCD-MOROZOFF";
    public string Password { get; set; } = string.Empty;
    public List<string> RemotePaths { get; set; } = new() { "/" };
    public int RetentionDays { get; set; } = 1;
    public bool EnableBackup { get; set; } = true;
    public string LocalBackupPath { get; set; } = Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, "Backups");
}
