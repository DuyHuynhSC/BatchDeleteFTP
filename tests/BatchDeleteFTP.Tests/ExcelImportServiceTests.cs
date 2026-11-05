using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using BatchDeleteFTP.Models;
using BatchDeleteFTP.Services;
using Xunit;

namespace BatchDeleteFTP.Tests;

public class ExcelImportServiceTests
{
    [Fact]
    public async Task ExportTemplate_And_ReadExcel_SuccessfullyReadsSampleRow()
    {
        string tempXlsx = Path.Combine(Path.GetTempPath(), $"template_test_{Guid.NewGuid()}.xlsx");
        try
        {
            var service = new ExcelImportService();
            await service.ExportTemplateAsync(tempXlsx);
            Assert.True(File.Exists(tempXlsx));

            var entries = await service.ReadExcelAsync(tempXlsx);
            Assert.NotEmpty(entries);

            var first = entries[0];
            Assert.Equal("172.16.6.16", first.Ip);
            Assert.Equal("D2-SVR15-6016", first.MachineName);
            Assert.Equal("FUJINET.vn", first.DomainOrWorkgroup);
            Assert.Equal("Administrator", first.Username);
            Assert.Equal("D2-20100802", first.Password);
            Assert.Equal("ORCL11GR2K", first.Tns);
        }
        finally
        {
            if (File.Exists(tempXlsx)) File.Delete(tempXlsx);
        }
    }

    [Fact]
    public void MergeIntoList_OverwriteExisting_UpdatesExistingEntry()
    {
        var service = new ExcelImportService();
        var current = new List<VirtualMachineEntry>
        {
            new()
            {
                Id = "id-1",
                Ip = "172.16.6.16",
                MachineName = "OLD-NAME",
                Password = "OldPassword"
            }
        };

        var incoming = new List<VirtualMachineEntry>
        {
            new()
            {
                Ip = "172.16.6.16",
                MachineName = "NEW-NAME",
                Password = "NewPassword123"
            }
        };

        var result = service.MergeIntoList(current, incoming, ExcelImportConflictAction.OverwriteExisting);

        Assert.Equal(1, result.UpdatedCount);
        Assert.Equal(0, result.AddedCount);
        Assert.Single(current);
        Assert.Equal("NEW-NAME", current[0].MachineName);
        Assert.Equal("NewPassword123", current[0].Password);
    }

    [Fact]
    public void MergeIntoList_SkipExisting_KeepsOriginalEntry()
    {
        var service = new ExcelImportService();
        var current = new List<VirtualMachineEntry>
        {
            new()
            {
                Id = "id-1",
                Ip = "172.16.6.16",
                MachineName = "OLD-NAME"
            }
        };

        var incoming = new List<VirtualMachineEntry>
        {
            new()
            {
                Ip = "172.16.6.16",
                MachineName = "NEW-NAME"
            }
        };

        var result = service.MergeIntoList(current, incoming, ExcelImportConflictAction.SkipExisting);

        Assert.Equal(1, result.SkippedCount);
        Assert.Equal(0, result.UpdatedCount);
        Assert.Single(current);
        Assert.Equal("OLD-NAME", current[0].MachineName);
    }

    [Fact]
    public void MergeIntoList_AppendAll_AppendsDuplicateAsNewRow()
    {
        var service = new ExcelImportService();
        var current = new List<VirtualMachineEntry>
        {
            new() { Id = "id-1", Ip = "172.16.6.16", MachineName = "M1" }
        };

        var incoming = new List<VirtualMachineEntry>
        {
            new() { Ip = "172.16.6.16", MachineName = "M2" }
        };

        var result = service.MergeIntoList(current, incoming, ExcelImportConflictAction.AppendAll);

        Assert.Equal(1, result.AddedCount);
        Assert.Equal(2, current.Count);
    }
}
