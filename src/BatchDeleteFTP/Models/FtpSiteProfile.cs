using System;
using System.Collections.Generic;
using System.IO;

namespace BatchDeleteFTP.Models;

public class FtpSiteProfile
{
    public string Name { get; set; } = "Default";
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
    public string LocalBackupPath { get; set; } = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Backups");

    // Email Notification Settings per Profile
    public string SmtpHost { get; set; } = "smtp.gmail.com";
    public int SmtpPort { get; set; } = 587;
    public bool SmtpEnableSsl { get; set; } = true;
    public string SmtpUser { get; set; } = string.Empty;
    public string SmtpPassword { get; set; } = string.Empty;
    public string SenderEmail { get; set; } = string.Empty;
    public List<string> RecipientEmails { get; set; } = new();
    public bool AutoSendEmailOnPreview { get; set; } = false;

    public static FtpSiteProfile FromSettings(string name, FtpSettings s) => new()
    {
        Name = string.IsNullOrWhiteSpace(name) ? "Default" : name.Trim(),
        Host = s.Host,
        Port = s.Port,
        EncryptionMode = s.EncryptionMode,
        Username = s.Username,
        Password = s.Password,
        UseProxy = s.UseProxy,
        ProxyHost = s.ProxyHost,
        ProxyPort = s.ProxyPort,
        ProxyUser = s.ProxyUser,
        ProxyPassword = s.ProxyPassword,
        RemotePaths = new List<string>(s.RemotePaths != null && s.RemotePaths.Count > 0 ? s.RemotePaths : new List<string> { "/" }),
        RetentionDays = s.RetentionDays,
        EnableBackup = s.EnableBackup,
        LocalBackupPath = s.LocalBackupPath,
        SmtpHost = s.SmtpHost,
        SmtpPort = s.SmtpPort,
        SmtpEnableSsl = s.SmtpEnableSsl,
        SmtpUser = s.SmtpUser,
        SmtpPassword = s.SmtpPassword,
        SenderEmail = s.SenderEmail,
        RecipientEmails = new List<string>(s.RecipientEmails ?? new List<string>()),
        AutoSendEmailOnPreview = s.AutoSendEmailOnPreview
    };

    public void ApplyTo(FtpSettings s)
    {
        s.SelectedProfileName = Name;
        s.Host = Host;
        s.Port = Port;
        s.EncryptionMode = EncryptionMode;
        s.Username = Username;
        s.Password = Password;
        s.UseProxy = UseProxy;
        s.ProxyHost = ProxyHost;
        s.ProxyPort = ProxyPort;
        s.ProxyUser = ProxyUser;
        s.ProxyPassword = ProxyPassword;
        s.RemotePaths = new List<string>(RemotePaths != null && RemotePaths.Count > 0 ? RemotePaths : new List<string> { "/" });
        s.RetentionDays = RetentionDays;
        s.EnableBackup = EnableBackup;
        s.LocalBackupPath = LocalBackupPath;
        s.SmtpHost = SmtpHost;
        s.SmtpPort = SmtpPort;
        s.SmtpEnableSsl = SmtpEnableSsl;
        s.SmtpUser = SmtpUser;
        s.SmtpPassword = SmtpPassword;
        s.SenderEmail = SenderEmail;
        s.RecipientEmails = new List<string>(RecipientEmails ?? new List<string>());
        s.AutoSendEmailOnPreview = AutoSendEmailOnPreview;
    }

    public override string ToString() => Name;
}
