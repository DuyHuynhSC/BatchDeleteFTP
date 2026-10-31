using BatchDeleteFTP.Models;
using BatchDeleteFTP.Services;
using Xunit;

namespace BatchDeleteFTP.Tests;

public class FileManagerServiceTests
{
    [Theory]
    [InlineData("", "/")]
    [InlineData("/", "/")]
    [InlineData("folder", "/folder")]
    [InlineData("/folder/", "/folder")]
    [InlineData("\\folder\\sub\\", "/folder/sub")]
    [InlineData("folder/sub", "/folder/sub")]
    public void NormalizeSingleRemotePath_WorksCorrectly(string input, string expected)
    {
        var result = FtpCleanupService.NormalizeSingleRemotePath(input);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("/", "/")]
    [InlineData("/folder", "/")]
    [InlineData("/folder/sub", "/folder")]
    [InlineData("/folder/sub/child", "/folder/sub")]
    public void GetParentRemotePath_WorksCorrectly(string input, string expected)
    {
        var result = FtpCleanupService.GetParentRemotePath(input);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("/", "sub", "/sub")]
    [InlineData("/folder", "file.txt", "/folder/file.txt")]
    [InlineData("/folder/", "/file.txt", "/folder/file.txt")]
    [InlineData("/folder", "sub/file.txt", "/folder/sub/file.txt")]
    public void CombineRemotePath_WorksCorrectly(string baseDir, string child, string expected)
    {
        var result = FtpCleanupService.CombineRemotePath(baseDir, child);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(0, "0 B")]
    [InlineData(500, "500 B")]
    [InlineData(1024, "1.0 KB (1,024)")]
    [InlineData(1048576, "1.00 MB")]
    [InlineData(1073741824, "1.00 GB")]
    public void FormatFileSize_FormatsProperly(long bytes, string expected)
    {
        var result = FileManagerEntry.FormatFileSize(bytes);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void FileManagerEntry_IconAndType_DetectProperly()
    {
        var parent = new FileManagerEntry { Name = "..", IsParentEntry = true, IsDirectory = true };
        Assert.Equal("FolderArrowUpOutline", parent.IconKind);
        Assert.Equal("Thư mục cha", parent.TypeLabel);
        Assert.Equal("<DIR>", parent.SizeFormatted);

        var dir = new FileManagerEntry { Name = "uploads", IsDirectory = true };
        Assert.Equal("Folder", dir.IconKind);
        Assert.Equal("Thư mục", dir.TypeLabel);

        var txtFile = new FileManagerEntry { Name = "log.txt", Size = 2048 };
        Assert.Equal("FileDocumentOutline", txtFile.IconKind);
        Assert.Equal("TXT File", txtFile.TypeLabel);

        var zipFile = new FileManagerEntry { Name = "data.zip" };
        Assert.Equal("FolderZipOutline", zipFile.IconKind);
        Assert.Equal("ZIP File", zipFile.TypeLabel);
    }
}
