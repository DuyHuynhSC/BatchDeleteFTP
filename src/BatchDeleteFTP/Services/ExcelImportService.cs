using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using BatchDeleteFTP.Models;
using MiniExcelLibs;

namespace BatchDeleteFTP.Services;

public enum ExcelImportConflictAction
{
    OverwriteExisting,
    SkipExisting,
    AppendAll
}

public class ExcelImportResult
{
    public int TotalRead { get; set; }
    public int AddedCount { get; set; }
    public int UpdatedCount { get; set; }
    public int SkippedCount { get; set; }
    public List<string> Errors { get; } = new();
    public List<VirtualMachineEntry> ImportedEntries { get; } = new();
}

public class ExcelImportService
{
    public Task<List<VirtualMachineEntry>> ReadExcelAsync(string filePath)
    {
        return Task.Run(() =>
        {
            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException("Không tìm thấy file Excel", filePath);
            }

            var rows = MiniExcel.Query(filePath, useHeaderRow: true).ToList();
            var list = new List<VirtualMachineEntry>();

            foreach (IDictionary<string, object> row in rows)
            {
                // Bỏ qua dòng hoàn toàn rỗng
                if (row.Values.All(v => v == null || string.IsNullOrWhiteSpace(v.ToString())))
                {
                    continue;
                }

                var entry = new VirtualMachineEntry
                {
                    No = GetValue(row, "no", "stt", "id"),
                    Ip = GetValue(row, "ip", "địa chỉ ip", "ip address"),
                    MachineName = GetValue(row, "tên máy", "machine name", "hostname", "host name", "tên server", "server name"),
                    DomainOrWorkgroup = GetValue(row, "domain/workgroup", "domain", "workgroup"),
                    Os = GetValue(row, "os", "hệ điều hành", "operating system"),
                    Username = GetValue(row, "user", "username", "tài khoản", "người dùng"),
                    Password = GetValue(row, "p", "password", "pass", "mật khẩu"),
                    Tns = GetValue(row, "tns", "tns name"),
                    DatabaseName = GetValue(row, "database name", "database", "db name", "tên db"),
                    OracleVersion = GetValue(row, "version oracle", "oracle version", "phiên bản oracle"),
                    Tester = GetValue(row, "người kiểm tra", "người kiểm tra/quản lý", "tester", "quản trị"),
                    Notes = GetValue(row, "ghi chú", "note", "notes", "mô tả"),
                    ExpirationDate = GetValue(row, "ngày hết hạn", "hết hạn", "expire", "expiration date", "expired date")
                };

                if (string.IsNullOrWhiteSpace(entry.ExpirationDate))
                {
                    entry.ExpirationDate = "Vô hạn";
                }

                // Nếu có ít nhất IP hoặc Tên máy thì mới xem là dòng hợp lệ
                if (!string.IsNullOrWhiteSpace(entry.Ip) || !string.IsNullOrWhiteSpace(entry.MachineName))
                {
                    list.Add(entry);
                }
            }

            return list;
        });
    }

    public ExcelImportResult MergeIntoList(
        List<VirtualMachineEntry> currentList,
        IEnumerable<VirtualMachineEntry> importedList,
        ExcelImportConflictAction conflictAction)
    {
        var result = new ExcelImportResult();

        foreach (var incoming in importedList)
        {
            result.TotalRead++;

            // Tìm máy trùng theo IP (hoặc Tên máy nếu IP trống)
            var existing = currentList.FirstOrDefault(e =>
                (!string.IsNullOrWhiteSpace(e.Ip) && string.Equals(e.Ip.Trim(), incoming.Ip.Trim(), StringComparison.OrdinalIgnoreCase)) ||
                (!string.IsNullOrWhiteSpace(e.MachineName) && string.Equals(e.MachineName.Trim(), incoming.MachineName.Trim(), StringComparison.OrdinalIgnoreCase)));

            if (existing != null)
            {
                if (conflictAction == ExcelImportConflictAction.SkipExisting)
                {
                    result.SkippedCount++;
                }
                else if (conflictAction == ExcelImportConflictAction.OverwriteExisting)
                {
                    existing.No = string.IsNullOrWhiteSpace(incoming.No) ? existing.No : incoming.No;
                    existing.MachineName = string.IsNullOrWhiteSpace(incoming.MachineName) ? existing.MachineName : incoming.MachineName;
                    existing.DomainOrWorkgroup = string.IsNullOrWhiteSpace(incoming.DomainOrWorkgroup) ? existing.DomainOrWorkgroup : incoming.DomainOrWorkgroup;
                    existing.Os = string.IsNullOrWhiteSpace(incoming.Os) ? existing.Os : incoming.Os;
                    existing.Username = string.IsNullOrWhiteSpace(incoming.Username) ? existing.Username : incoming.Username;
                    existing.Password = string.IsNullOrWhiteSpace(incoming.Password) ? existing.Password : incoming.Password;
                    existing.Tns = string.IsNullOrWhiteSpace(incoming.Tns) ? existing.Tns : incoming.Tns;
                    existing.DatabaseName = string.IsNullOrWhiteSpace(incoming.DatabaseName) ? existing.DatabaseName : incoming.DatabaseName;
                    existing.OracleVersion = string.IsNullOrWhiteSpace(incoming.OracleVersion) ? existing.OracleVersion : incoming.OracleVersion;
                    existing.Tester = string.IsNullOrWhiteSpace(incoming.Tester) ? existing.Tester : incoming.Tester;
                    existing.Notes = string.IsNullOrWhiteSpace(incoming.Notes) ? existing.Notes : incoming.Notes;
                    existing.ExpirationDate = string.IsNullOrWhiteSpace(incoming.ExpirationDate) ? existing.ExpirationDate : incoming.ExpirationDate;

                    result.UpdatedCount++;
                    result.ImportedEntries.Add(existing);
                }
                else // AppendAll
                {
                    var newEntry = incoming.Clone();
                    newEntry.Id = Guid.NewGuid().ToString();
                    currentList.Add(newEntry);
                    result.AddedCount++;
                    result.ImportedEntries.Add(newEntry);
                }
            }
            else
            {
                var newEntry = incoming.Clone();
                if (string.IsNullOrEmpty(newEntry.Id)) newEntry.Id = Guid.NewGuid().ToString();
                currentList.Add(newEntry);
                result.AddedCount++;
                result.ImportedEntries.Add(newEntry);
            }
        }

        return result;
    }

    public async Task ExportTemplateAsync(string destinationPath)
    {
        var sampleRows = new[]
        {
            new
            {
                No = "1",
                ip = "172.16.6.16",
                Tên_máy = "D2-SVR15-6016",
                Domain_Workgroup = "FUJINET.vn",
                OS = "Windows Server 2008 64bit",
                user = "Administrator",
                P = "D2-20100802",
                TNS = "ORCL11GR2K",
                Database_Name = "ORCL11GR2K",
                Version_Oracle = "11.2.0.4.0",
                Người_kiểm_tra = "Duy",
                Ghi_chú = "Oracle Server 11gr2",
                Ngày_hết_hạn = "Vô hạn"
            }
        };

        await Task.Run(() =>
        {
            MiniExcel.SaveAs(destinationPath, sampleRows, overwriteFile: true);
        });
    }

    private static string GetValue(IDictionary<string, object> dict, params string[] possibleKeys)
    {
        foreach (var key in possibleKeys)
        {
            // Normalize key for matching
            var entry = dict.FirstOrDefault(kv => NormalizeKey(kv.Key) == NormalizeKey(key));
            if (entry.Value != null)
            {
                string val = entry.Value.ToString()?.Trim() ?? string.Empty;
                if (!string.IsNullOrEmpty(val)) return val;
            }
        }
        return string.Empty;
    }

    private static string NormalizeKey(string? str)
    {
        if (string.IsNullOrWhiteSpace(str)) return string.Empty;
        return str.Trim().ToLowerInvariant().Replace("_", " ").Replace("-", " ").Replace("/", " ").Replace(".", "");
    }
}
