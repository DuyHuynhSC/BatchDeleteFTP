using System;
using System.IO;

namespace WorkManagement.Models;

public class ProjectCleanupSettings
{
    public string BackupRootDirectory { get; set; } = string.Empty;
    public bool EnableBackupFolder { get; set; } = true;
    public bool EnableBackupDatabase { get; set; } = true;
    public bool EnableDeleteFolder { get; set; } = true;
    public bool EnableDeleteUsers { get; set; } = true;
    public bool EnableDeleteTablespace { get; set; } = true;
    public string DefaultDbaUser { get; set; } = "SYSTEM";

    public ProjectCleanupSettings()
    {
        string defaultDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "WorkManagement_Backups");
        BackupRootDirectory = defaultDir;
    }
}
