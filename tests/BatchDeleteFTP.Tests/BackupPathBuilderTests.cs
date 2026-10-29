using System.IO;
using BatchDeleteFTP.Helpers;
using Xunit;

namespace BatchDeleteFTP.Tests;

public class BackupPathBuilderTests
{
    [Fact]
    public void BuildLocalBackupFilePath_PreservesRemoteDirectoryStructure()
    {
        string root = @"C:\Backups";
        string session = "20261028_153000";
        string remotePath = "/SITE_A/Outbound/2026/order_01.csv";

        string localPath = BackupPathBuilder.BuildLocalBackupFilePath(root, session, remotePath);

        string expected = Path.Combine(@"C:\Backups", "20261028_153000", "SITE_A", "Outbound", "2026", "order_01.csv");
        Assert.Equal(expected, localPath);
    }
}
