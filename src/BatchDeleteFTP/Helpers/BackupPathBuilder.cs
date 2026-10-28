using System;
using System.IO;

namespace BatchDeleteFTP.Helpers;

public static class BackupPathBuilder
{
    public static string BuildLocalBackupFilePath(string localBackupRoot, string sessionTimestamp, string remoteFilePath)
    {
        string normalizedRemote = (remoteFilePath ?? string.Empty)
            .Replace('\\', '/')
            .TrimStart('/');

        string[] segments = normalizedRemote.Split('/', StringSplitOptions.RemoveEmptyEntries);
        string baseDir = Path.Combine(localBackupRoot, sessionTimestamp);

        foreach (string segment in segments)
        {
            baseDir = Path.Combine(baseDir, segment);
        }

        return baseDir;
    }
}
