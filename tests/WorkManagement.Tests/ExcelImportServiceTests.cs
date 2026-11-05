using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using WorkManagement.Models;
using WorkManagement.Services;
using Xunit;

namespace WorkManagement.Tests;

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
                No = "17",
                Ip = "172.16.6.16",
                MachineName = "OLD-NAME",
                Password = "OldPassword"
            }
        };

        var incoming = new List<VirtualMachineEntry>
        {
            new()
            {
                No = "1",
                Ip = "172.16.6.16",
                MachineName = "NEW-NAME",
                Password = "NewPassword123"
            }
        };

        var result = service.MergeIntoList(current, incoming, ExcelImportConflictAction.OverwriteExisting);

        Assert.Equal(1, result.UpdatedCount);
        Assert.Equal(0, result.AddedCount);
        Assert.Single(current);
        Assert.Equal("1", current[0].No);
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

    [Fact]
    public async Task ReadExcel_HeaderOnSecondRowAndMultipleSheets_SuccessfullyReadsData()
    {
        string tempXlsx = Path.Combine(Path.GetTempPath(), $"multisheet_row2_{Guid.NewGuid()}.xlsx");
        try
        {
            var sheet1Rows = new List<Dictionary<string, object>>
            {
                new()
                {
                    { "A", "DANH SÁCH MÁY CHỦ REMOTE NỘI BỘ" }, { "B", "" }, { "C", "" }, { "D", "" },
                    { "E", "" }, { "F", "" }, { "G", "" }, { "H", "" }, { "I", "" }, { "J", "" },
                    { "K", "" }, { "L", "" }, { "M", "" }
                }, // Dòng 1: Tiêu đề
                new()
                {
                    { "A", "No" },
                    { "B", "ip" },
                    { "C", "Tên máy" },
                    { "D", "Domain/Workgroup" },
                    { "E", "OS" },
                    { "F", "user" },
                    { "G", "P" },
                    { "H", "TNS" },
                    { "I", "Database Name" },
                    { "J", "Version Oracl" },
                    { "K", "Người kiểm t" },
                    { "L", "Ghi chú" },
                    { "M", "Ngày hết hạn" }
                }, // Dòng 2: Header
                new()
                {
                    { "A", "1" },
                    { "B", "172.16.7.31" },
                    { "C", "TECH-6984E35E4E" },
                    { "D", "WORKGROUP" },
                    { "E", "Windows 2000 Server Service Pack 4" },
                    { "F", "sc" },
                    { "G", "Xyz09876" },
                    { "H", "ORCL8I" },
                    { "I", "ORCL8I.WORLD" },
                    { "J", "" },
                    { "K", "Hùng" },
                    { "L", "Oracle Server 8i" },
                    { "M", "Vô hạn" }
                },
                new()
                {
                    { "A", "2" },
                    { "B", "172.16.0.55" },
                    { "C", "DEPT2-DB02" },
                    { "D", "FUJINET.vn" },
                    { "E", "Windows Server 2008 R2 64bit" },
                    { "F", "Administrator" },
                    { "G", "D2-20100802" },
                    { "H", "ORCL11GR2F" },
                    { "I", "ORCL11GR2F" },
                    { "J", "11.2.0.2.0" },
                    { "K", "KTrieu" },
                    { "L", "Oracle Server 11gr2" },
                    { "M", "2020/12/31" }
                }
            };

            var sheets = new Dictionary<string, object>
            {
                { "RemoteInfo", sheet1Rows },
                { "SummarySheet", new List<object> { new { Col = "Other" } } }
            };

            MiniExcelLibs.MiniExcel.SaveAs(tempXlsx, sheets, overwriteFile: true);

            var service = new ExcelImportService();
            var entries = await service.ReadExcelAsync(tempXlsx);

            Assert.NotNull(entries);
            Assert.Equal(2, entries.Count);

            var vm1 = entries[0];
            Assert.Equal("1", vm1.No);
            Assert.Equal("172.16.7.31", vm1.Ip);
            Assert.Equal("TECH-6984E35E4E", vm1.MachineName);
            Assert.Equal("WORKGROUP", vm1.DomainOrWorkgroup);
            Assert.Equal("Windows 2000 Server Service Pack 4", vm1.Os);
            Assert.Equal("sc", vm1.Username);
            Assert.Equal("Xyz09876", vm1.Password);
            Assert.Equal("ORCL8I", vm1.Tns);
            Assert.Equal("ORCL8I.WORLD", vm1.DatabaseName);
            Assert.Equal("Hùng", vm1.Tester);
            Assert.Equal("Oracle Server 8i", vm1.Notes);

            var vm2 = entries[1];
            Assert.Equal("2", vm2.No);
            Assert.Equal("172.16.0.55", vm2.Ip);
            Assert.Equal("DEPT2-DB02", vm2.MachineName);
            Assert.Equal("11.2.0.2.0", vm2.OracleVersion);
            Assert.Equal("KTrieu", vm2.Tester);
            Assert.Equal("2020/12/31", vm2.ExpirationDate);
        }
        finally
        {
            if (File.Exists(tempXlsx)) File.Delete(tempXlsx);
        }
    }

    [Fact]
    public async Task ReadSheetsFromExcel_WhenDbUsersSheetIsFirst_PrioritizesRemoteVmsSheet()
    {
        string tempXlsx = Path.Combine(Path.GetTempPath(), $"multisheet_db_first_{Guid.NewGuid()}.xlsx");
        try
        {
            // Sheet 1: DB Users (giống trường hợp lỗi thực tế của người dùng)
            var dbUsersSheet = new List<Dictionary<string, object>>
            {
                new()
                {
                    { "A", "STT" }, { "B", "Đánh dấu" }, { "C", "Tên máy" }, { "D", "User" },
                    { "E", "Password" }, { "F", "TNS" }, { "G", "Ghi chú" }
                },
                new()
                {
                    { "A", "" }, { "B", "*" }, { "C", "FJN-TEST-2" }, { "D", "TSH_RPS" },
                    { "E", "ORCL10-1" }, { "F", "ORCL10" }, { "G", "Schema test" }
                }
            };

            // Sheet 2: Danh sách máy ảo Remote đầy đủ cột
            var remoteVmsSheet = new List<Dictionary<string, object>>
            {
                new()
                {
                    { "A", "No" }, { "B", "ip" }, { "C", "Tên máy" }, { "D", "Domain/Workgroup" },
                    { "E", "OS" }, { "F", "user" }, { "G", "P" }, { "H", "TNS" },
                    { "I", "Database Name" }, { "J", "Version Oracle" }, { "K", "Người kiểm tra" },
                    { "L", "Ghi chú" }, { "M", "Ngày hết hạn" }
                },
                new()
                {
                    { "A", "1" }, { "B", "172.16.7.31" }, { "C", "TECH-6984E35E4E" }, { "D", "WORKGROUP" },
                    { "E", "Windows 2000 Server" }, { "F", "sc" }, { "G", "Xyz09876" }, { "H", "ORCL8I" },
                    { "I", "ORCL8I.WORLD" }, { "J", "8.1.7" }, { "K", "Hùng" },
                    { "L", "Oracle Server 8i" }, { "M", "Vô hạn" }
                }
            };

            var workbook = new Dictionary<string, object>
            {
                { "DB_Users", dbUsersSheet },
                { "Remote_VMs", remoteVmsSheet }
            };

            MiniExcelLibs.MiniExcel.SaveAs(tempXlsx, workbook, overwriteFile: true);

            var service = new ExcelImportService();
            var sheets = await service.ReadSheetsFromExcelAsync(tempXlsx);

            // Phải đọc được cả 2 sheet
            Assert.Equal(2, sheets.Count);

            // Sheet Remote_VMs phải có điểm cao nhất và được chọn làm đề xuất (IsRecommended = true)
            var topSheet = sheets[0];
            Assert.Equal("Remote_VMs", topSheet.SheetName);
            Assert.True(topSheet.IsRecommended);
            Assert.Single(topSheet.Entries);

            var entry = topSheet.Entries[0];
            Assert.Equal("172.16.7.31", entry.Ip);
            Assert.Equal("TECH-6984E35E4E", entry.MachineName);
            Assert.Equal("WORKGROUP", entry.DomainOrWorkgroup);
            Assert.Equal("sc", entry.Username);
            Assert.Equal("Xyz09876", entry.Password);

            // ReadExcelAsync cũng phải tự động trả về sheet Remote_VMs
            var defaultEntries = await service.ReadExcelAsync(tempXlsx);
            Assert.Single(defaultEntries);
            Assert.Equal("172.16.7.31", defaultEntries[0].Ip);
        }
        finally
        {
            if (File.Exists(tempXlsx)) File.Delete(tempXlsx);
        }
    }
}
