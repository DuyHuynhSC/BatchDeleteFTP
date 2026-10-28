using BatchDeleteFTP.Services;
using Xunit;

namespace BatchDeleteFTP.Tests;

public class RemotePathNormalizerTests
{
    [Fact]
    public void NormalizeRemotePaths_RemovesDuplicatesAndChildRedundancy()
    {
        var raw = new[] { "FolderA", "/FolderA/", "/FolderA/Sub1", "/FolderB" };

        var normalized = FtpCleanupService.NormalizeRemotePaths(raw);

        Assert.Equal(new[] { "/FolderA", "/FolderB" }, normalized);
    }
}
