using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using WorkManagement.Helpers;
using WorkManagement.Models;
using WorkManagement.Services;
using WorkManagement.Views;
using Xunit;

namespace WorkManagement.Tests;

public class DateFilterRuleTests
{
    [Fact]
    public void ShouldDelete_WhenSystemDateIsGreaterThanUploadDatePlusOne_ReturnsTrue()
    {
        var uploadDate = new DateTime(2026, 10, 26, 23, 50, 0);
        var systemDate = new DateTime(2026, 10, 28, 08, 15, 0);

        bool result = DateFilterRule.ShouldDelete(uploadDate, systemDate, retentionDays: 1);

        Assert.True(result);
    }

    [Fact]
    public void ShouldDelete_WhenSystemDateEqualsUploadDatePlusOne_ReturnsFalse()
    {
        var uploadDate = new DateTime(2026, 10, 27, 01, 00, 0);
        var systemDate = new DateTime(2026, 10, 28, 22, 00, 0);

        bool result = DateFilterRule.ShouldDelete(uploadDate, systemDate, retentionDays: 1);

        Assert.False(result);
    }

    [Fact]
    public void ShouldDelete_WhenUploadedToday_ReturnsFalse()
    {
        var uploadDate = new DateTime(2026, 10, 28, 10, 00, 0);
        var systemDate = new DateTime(2026, 10, 28, 15, 00, 0);

        bool result = DateFilterRule.ShouldDelete(uploadDate, systemDate, retentionDays: 1);

        Assert.False(result);
    }

    [Fact]
    public void ShouldDeleteCandidate_RespectsExcludePatterns_IncludeExtensions_And_MinSize()
    {
        var uploadDate = new DateTime(2026, 10, 20, 10, 0, 0);
        var systemDate = new DateTime(2026, 10, 28, 10, 0, 0);

        var settings = new FtpSettings
        {
            RetentionDays = 1,
            ExcludePatterns = "*.keep;readme.txt;/master/",
            IncludeExtensions = ".zip;.dmp",
            MinFileSizeMb = 5.0 // 5 MB
        };

        long sixMb = 6L * 1024 * 1024;
        long twoMb = 2L * 1024 * 1024;

        // 1. Thỏa mãn đầy đủ: quá hạn, .zip, 6MB, không nằm trong exclude
        Assert.True(DateFilterRule.ShouldDeleteCandidate("/upload/data_backup.zip", "data_backup.zip", sixMb, uploadDate, systemDate, settings));

        // 2. Bị chặn bởi ExcludePatterns (*.keep hoặc /master/)
        Assert.False(DateFilterRule.ShouldDeleteCandidate("/upload/important.keep", "important.keep", sixMb, uploadDate, systemDate, settings));
        Assert.False(DateFilterRule.ShouldDeleteCandidate("/master/schema.dmp", "schema.dmp", sixMb, uploadDate, systemDate, settings));
        Assert.True(DateFilterRule.IsExcludedDirectory("/upload/master/sub", settings.ExcludePatterns));

        // 3. Bị chặn bởi IncludeExtensions (.txt không nằm trong .zip;.dmp)
        Assert.False(DateFilterRule.ShouldDeleteCandidate("/upload/notes.txt", "notes.txt", sixMb, uploadDate, systemDate, settings));

        // 4. Bị chặn bởi MinFileSizeMb (2MB < 5MB)
        Assert.False(DateFilterRule.ShouldDeleteCandidate("/upload/small.zip", "small.zip", twoMb, uploadDate, systemDate, settings));
    }

    [Fact]
    public async Task ExportVmList_And_ExportProjects_GeneratesValidExcelFiles()
    {
        string tempVmExcel = Path.Combine(Path.GetTempPath(), $"export_vm_{Guid.NewGuid():N}.xlsx");
        string tempProjExcel = Path.Combine(Path.GetTempPath(), $"export_proj_{Guid.NewGuid():N}.xlsx");

        try
        {
            var vmExcelService = new ExcelImportService();
            await vmExcelService.ExportVmListAsync(tempVmExcel, new List<VirtualMachineEntry>
            {
                new() { No = "1", Ip = "172.16.6.10", MachineName = "SVR-10", Username = "Administrator" }
            });
            Assert.True(File.Exists(tempVmExcel));

            var readBackVms = await vmExcelService.ReadExcelAsync(tempVmExcel);
            Assert.Single(readBackVms);
            Assert.Equal("172.16.6.10", readBackVms[0].Ip);

            var projExcelService = new ProjectExcelService();
            await projExcelService.ExportProjectsToExcelAsync(tempProjExcel, new List<ProjectEntry>
            {
                new() { No = "200", ProjectName = "DEMO_EXPORT", SourceFolder = @"Q:\DEMO", HostServer = "172.16.6.10" }
            });
            Assert.True(File.Exists(tempProjExcel));

            var readBackProjs = await projExcelService.ReadProjectsFromExcelAsync(tempProjExcel);
            Assert.Single(readBackProjs);
            Assert.Equal("DEMO_EXPORT", readBackProjs[0].ProjectName);
        }
        finally
        {
            if (File.Exists(tempVmExcel)) File.Delete(tempVmExcel);
            if (File.Exists(tempProjExcel)) File.Delete(tempProjExcel);
        }
    }

    [Fact]
    public void DashboardView_HelperMethods_ParseExpiration_And_CleanOldFiles()
    {
        Assert.False(DashboardView.TryParseExpirationDate("Vô hạn", out _));
        Assert.True(DashboardView.TryParseExpirationDate("2026/12/31", out DateTime parsed));
        Assert.Equal(new DateTime(2026, 12, 31), parsed);

        string tempCleanDir = Path.Combine(Path.GetTempPath(), $"clean_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempCleanDir);
        try
        {
            string oldFile = Path.Combine(tempCleanDir, "old.bak");
            string newFile = Path.Combine(tempCleanDir, "new.bak");
            File.WriteAllText(oldFile, "OLD_DATA_CONTENT");
            File.WriteAllText(newFile, "NEW_DATA_CONTENT");

            File.SetLastWriteTime(oldFile, DateTime.Now.AddDays(-45));
            File.SetLastWriteTime(newFile, DateTime.Now.AddDays(-2));

            var (deleted, freed) = DashboardView.CleanDirectoryOlderThan(tempCleanDir, DateTime.Now.AddDays(-30));
            Assert.Equal(1, deleted);
            Assert.True(freed > 0);
            Assert.False(File.Exists(oldFile));
            Assert.True(File.Exists(newFile));
        }
        finally
        {
            if (Directory.Exists(tempCleanDir)) Directory.Delete(tempCleanDir, recursive: true);
        }
    }
}
