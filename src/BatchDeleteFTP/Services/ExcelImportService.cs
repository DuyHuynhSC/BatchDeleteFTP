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

            // Lấy danh sách tất cả các sheet trong file Excel
            List<string> sheetNames;
            try
            {
                sheetNames = MiniExcel.GetSheetNames(filePath).ToList();
            }
            catch
            {
                sheetNames = new List<string>();
            }

            if (sheetNames.Count == 0)
            {
                sheetNames.Add(string.Empty); // Fallback query default/first sheet
            }

            // Quét từng sheet cho đến khi tìm thấy sheet chứa dữ liệu hợp lệ
            foreach (var sheetName in sheetNames)
            {
                var entries = ReadEntriesFromSheet(filePath, string.IsNullOrEmpty(sheetName) ? null : sheetName);
                if (entries.Count > 0)
                {
                    return entries;
                }
            }

            // Fallback cuối cùng: query mặc định
            return ReadEntriesFromSheet(filePath, null);
        });
    }

    private List<VirtualMachineEntry> ReadEntriesFromSheet(string filePath, string? sheetName)
    {
        var list = new List<VirtualMachineEntry>();
        var rawRows = new List<IDictionary<string, object>>();

        try
        {
            var queryResult = (sheetName == null
                ? MiniExcel.Query(filePath, useHeaderRow: false)
                : MiniExcel.Query(filePath, useHeaderRow: false, sheetName: sheetName));

            foreach (var row in queryResult)
            {
                if (row is IDictionary<string, object> dict)
                {
                    rawRows.Add(dict);
                }
            }
        }
        catch
        {
            return list;
        }

        if (rawRows.Count == 0) return list;

        int headerRowIndex = -1;
        var colMapping = new Dictionary<string, string>(); // ColumnKey (ví dụ "A", "B") -> Property Name

        // Quét tìm dòng tiêu đề Header (tìm trong tối đa 20 dòng đầu)
        int searchLimit = Math.Min(20, rawRows.Count);
        for (int i = 0; i < searchLimit; i++)
        {
            var row = rawRows[i];
            var detected = DetectHeaderRow(row);
            if (detected.Count >= 2) // Chứa ít nhất 2 cột nhận diện hợp lệ (ví dụ IP, Tên máy...)
            {
                headerRowIndex = i;
                colMapping = detected;
                break;
            }
        }

        if (headerRowIndex == -1 || colMapping.Count == 0)
        {
            return list;
        }

        // Đọc tất cả các dòng dữ liệu nằm sau dòng Header
        for (int i = headerRowIndex + 1; i < rawRows.Count; i++)
        {
            var row = rawRows[i];

            // Bỏ qua dòng rỗng hoàn toàn
            if (row.Values.All(v => v == null || string.IsNullOrWhiteSpace(v.ToString())))
            {
                continue;
            }

            var entry = new VirtualMachineEntry();

            foreach (var mapping in colMapping)
            {
                string colKey = mapping.Key;
                string propName = mapping.Value;

                if (!row.TryGetValue(colKey, out var rawVal) || rawVal == null)
                {
                    continue;
                }

                string valStr = FormatCellValue(rawVal);
                if (string.IsNullOrWhiteSpace(valStr)) continue;

                switch (propName)
                {
                    case nameof(VirtualMachineEntry.No):
                        entry.No = valStr;
                        break;
                    case nameof(VirtualMachineEntry.Ip):
                        entry.Ip = valStr;
                        break;
                    case nameof(VirtualMachineEntry.MachineName):
                        entry.MachineName = valStr;
                        break;
                    case nameof(VirtualMachineEntry.DomainOrWorkgroup):
                        entry.DomainOrWorkgroup = valStr;
                        break;
                    case nameof(VirtualMachineEntry.Os):
                        entry.Os = valStr;
                        break;
                    case nameof(VirtualMachineEntry.Username):
                        entry.Username = valStr;
                        break;
                    case nameof(VirtualMachineEntry.Password):
                        entry.Password = valStr;
                        break;
                    case nameof(VirtualMachineEntry.Tns):
                        entry.Tns = valStr;
                        break;
                    case nameof(VirtualMachineEntry.DatabaseName):
                        entry.DatabaseName = valStr;
                        break;
                    case nameof(VirtualMachineEntry.OracleVersion):
                        entry.OracleVersion = valStr;
                        break;
                    case nameof(VirtualMachineEntry.Tester):
                        entry.Tester = valStr;
                        break;
                    case nameof(VirtualMachineEntry.Notes):
                        entry.Notes = valStr;
                        break;
                    case nameof(VirtualMachineEntry.ExpirationDate):
                        entry.ExpirationDate = valStr;
                        break;
                }
            }

            if (string.IsNullOrWhiteSpace(entry.ExpirationDate))
            {
                entry.ExpirationDate = "Vô hạn";
            }

            // Dòng được xem là hợp lệ nếu có ít nhất IP hoặc Tên máy
            if (!string.IsNullOrWhiteSpace(entry.Ip) || !string.IsNullOrWhiteSpace(entry.MachineName))
            {
                list.Add(entry);
            }
        }

        return list;
    }

    private static Dictionary<string, string> DetectHeaderRow(IDictionary<string, object> row)
    {
        var mapping = new Dictionary<string, string>();

        foreach (var kv in row)
        {
            if (kv.Value == null) continue;
            string text = kv.Value.ToString()?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(text)) continue;

            string normalized = NormalizeKey(text);

            if (MatchAny(normalized, "no", "stt", "id"))
                mapping[kv.Key] = nameof(VirtualMachineEntry.No);
            else if (MatchAny(normalized, "ip", "dia chi ip", "ip address"))
                mapping[kv.Key] = nameof(VirtualMachineEntry.Ip);
            else if (MatchAny(normalized, "ten may", "machine name", "hostname", "host name", "ten server", "server name", "machine"))
                mapping[kv.Key] = nameof(VirtualMachineEntry.MachineName);
            else if (MatchAny(normalized, "domain workgroup", "domain", "workgroup", "domainworkgroup"))
                mapping[kv.Key] = nameof(VirtualMachineEntry.DomainOrWorkgroup);
            else if (MatchAny(normalized, "os", "he dieu hanh", "operating system"))
                mapping[kv.Key] = nameof(VirtualMachineEntry.Os);
            else if (MatchAny(normalized, "user", "username", "tai khoan", "nguoi dung"))
                mapping[kv.Key] = nameof(VirtualMachineEntry.Username);
            else if (MatchAny(normalized, "p", "password", "pass", "mat khau"))
                mapping[kv.Key] = nameof(VirtualMachineEntry.Password);
            else if (MatchAny(normalized, "tns", "tns name"))
                mapping[kv.Key] = nameof(VirtualMachineEntry.Tns);
            else if (MatchAny(normalized, "database name", "database", "db name", "ten db", "dbname"))
                mapping[kv.Key] = nameof(VirtualMachineEntry.DatabaseName);
            else if (MatchAny(normalized, "version oracle", "oracle version", "phien ban oracle", "version oracl"))
                mapping[kv.Key] = nameof(VirtualMachineEntry.OracleVersion);
            else if (MatchAny(normalized, "nguoi kiem tra", "nguoi kiem tra quan ly", "tester", "quan tri", "nguoi kt", "nguoi kiem t"))
                mapping[kv.Key] = nameof(VirtualMachineEntry.Tester);
            else if (MatchAny(normalized, "ghi chu", "note", "notes", "mo ta"))
                mapping[kv.Key] = nameof(VirtualMachineEntry.Notes);
            else if (MatchAny(normalized, "ngay het han", "het han", "expire", "expiration date", "expired date"))
                mapping[kv.Key] = nameof(VirtualMachineEntry.ExpirationDate);
        }

        return mapping;
    }

    private static string FormatCellValue(object val)
    {
        if (val is DateTime dt)
        {
            return dt.ToString("yyyy/MM/dd");
        }
        return val.ToString()?.Trim() ?? string.Empty;
    }

    private static bool MatchAny(string text, params string[] patterns)
    {
        return patterns.Any(p => text == p || text.StartsWith(p) || text.Contains(p));
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

    private static string NormalizeKey(string? str)
    {
        if (string.IsNullOrWhiteSpace(str)) return string.Empty;

        // Loại bỏ dấu tiếng Việt để so khớp dễ dàng
        string withoutDiacritics = RemoveDiacritics(str);

        return withoutDiacritics.Trim().ToLowerInvariant()
            .Replace("_", " ")
            .Replace("-", " ")
            .Replace("/", " ")
            .Replace(".", " ")
            .Replace(":", " ")
            .Replace("\r", " ")
            .Replace("\n", " ");
    }

    private static string RemoveDiacritics(string text)
    {
        var normalizedString = text.Normalize(System.Text.NormalizationForm.FormD);
        var stringBuilder = new System.Text.StringBuilder();

        foreach (var c in normalizedString)
        {
            var unicodeCategory = System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c);
            if (unicodeCategory != System.Globalization.UnicodeCategory.NonSpacingMark)
            {
                stringBuilder.Append(c);
            }
        }

        return stringBuilder.ToString().Normalize(System.Text.NormalizationForm.FormC)
            .Replace("đ", "d").Replace("Đ", "D");
    }
}
