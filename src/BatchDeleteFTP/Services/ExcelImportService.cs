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

public class ExcelSheetData
{
    public string SheetName { get; set; } = string.Empty;
    public List<VirtualMachineEntry> Entries { get; set; } = new();
    public int Score { get; set; }
    public bool IsRecommended { get; set; }

    public string DisplayText => IsRecommended
        ? $"{SheetName} ({Entries.Count} máy) [Khuyến nghị]"
        : $"{SheetName} ({Entries.Count} dòng)";
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
    public async Task<List<VirtualMachineEntry>> ReadExcelAsync(string filePath)
    {
        var sheets = await ReadSheetsFromExcelAsync(filePath);
        return sheets.Count > 0 ? sheets[0].Entries : new List<VirtualMachineEntry>();
    }

    public Task<List<ExcelSheetData>> ReadSheetsFromExcelAsync(string filePath)
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
                sheetNames.Add(string.Empty);
            }

            var sheetResults = new List<ExcelSheetData>();

            foreach (var sheetName in sheetNames)
            {
                var sheetData = ReadSheetData(filePath, string.IsNullOrEmpty(sheetName) ? null : sheetName);
                if (sheetData.Entries.Count > 0)
                {
                    sheetResults.Add(sheetData);
                }
            }

            // Nếu không tìm thấy bằng danh sách tên sheet, thử fallback query mặc định
            if (sheetResults.Count == 0)
            {
                var fallbackData = ReadSheetData(filePath, null);
                if (fallbackData.Entries.Count > 0)
                {
                    sheetResults.Add(fallbackData);
                }
            }

            // Sắp xếp các sheet theo điểm khớp (Score) giảm dần
            sheetResults = sheetResults.OrderByDescending(s => s.Score).ThenByDescending(s => s.Entries.Count).ToList();

            if (sheetResults.Count > 0)
            {
                sheetResults[0].IsRecommended = true;
            }

            return sheetResults;
        });
    }

    private ExcelSheetData ReadSheetData(string filePath, string? sheetName)
    {
        var result = new ExcelSheetData
        {
            SheetName = string.IsNullOrWhiteSpace(sheetName) ? "Sheet1" : sheetName
        };

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
            return result;
        }

        if (rawRows.Count == 0) return result;

        int headerRowIndex = -1;
        var colMapping = new Dictionary<string, string>(); // ColumnKey (ví dụ "A", "B") -> Property Name
        int bestScore = 0;

        // Quét tìm dòng tiêu đề Header (tìm trong tối đa 20 dòng đầu)
        int searchLimit = Math.Min(20, rawRows.Count);
        for (int i = 0; i < searchLimit; i++)
        {
            var row = rawRows[i];
            var (mapping, score) = DetectHeaderRow(row);
            if (score > bestScore && mapping.Count >= 2)
            {
                bestScore = score;
                headerRowIndex = i;
                colMapping = mapping;
            }
        }

        if (headerRowIndex == -1 || colMapping.Count == 0)
        {
            return result;
        }

        result.Score = bestScore;

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

            // Nếu file Excel không có cột No hoặc No bị trống, tự động gán theo số thứ tự dòng
            if (string.IsNullOrWhiteSpace(entry.No))
            {
                entry.No = (result.Entries.Count + 1).ToString();
            }

            // Dòng được xem là hợp lệ nếu có ít nhất IP hoặc Tên máy
            if (!string.IsNullOrWhiteSpace(entry.Ip) || !string.IsNullOrWhiteSpace(entry.MachineName))
            {
                result.Entries.Add(entry);
            }
        }

        return result;
    }

    private static (Dictionary<string, string> Mapping, int Score) DetectHeaderRow(IDictionary<string, object> row)
    {
        var mapping = new Dictionary<string, string>();
        int score = 0;

        foreach (var kv in row)
        {
            if (kv.Value == null) continue;
            string text = kv.Value.ToString()?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(text)) continue;

            string normalized = NormalizeKey(text);
            string[] words = normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries);

            string? prop = MatchProperty(normalized, words);
            if (prop != null && !mapping.ContainsValue(prop))
            {
                mapping[kv.Key] = prop;

                // Tính điểm chất lượng nhận diện của Sheet
                switch (prop)
                {
                    case nameof(VirtualMachineEntry.Ip):
                        score += 30;
                        break;
                    case nameof(VirtualMachineEntry.MachineName):
                        score += 25;
                        break;
                    case nameof(VirtualMachineEntry.Os):
                        score += 15;
                        break;
                    case nameof(VirtualMachineEntry.DomainOrWorkgroup):
                        score += 10;
                        break;
                    case nameof(VirtualMachineEntry.Username):
                        score += 10;
                        break;
                    case nameof(VirtualMachineEntry.Password):
                        score += 10;
                        break;
                    default:
                        score += 5;
                        break;
                }
            }
        }

        return (mapping, score);
    }

    private static string? MatchProperty(string norm, string[] words)
    {
        // NO / STT: so khớp chính xác
        if (norm == "no" || norm == "stt" || norm == "id" || norm == "no." || words.Contains("stt"))
            return nameof(VirtualMachineEntry.No);

        // IP: phải là 'ip', 'dia chi ip', 'ip address' hoặc từ 'ip' riêng biệt (tránh Description, Equipment, Equipment...)
        if (norm == "ip" || norm == "dia chi ip" || norm == "ip address" || words.Contains("ip"))
            return nameof(VirtualMachineEntry.Ip);

        // TÊN MÁY
        if (norm == "ten may" || norm == "machine name" || norm == "hostname" || norm == "host name" ||
            norm == "ten server" || norm == "server name" || norm == "machine" ||
            norm.Contains("ten may") || norm.Contains("machine name"))
            return nameof(VirtualMachineEntry.MachineName);

        // DOMAIN / WORKGROUP
        if (norm.Contains("domain") || norm.Contains("workgroup"))
            return nameof(VirtualMachineEntry.DomainOrWorkgroup);

        // OS / HỆ ĐIỀU HÀNH: so khớp chính xác hoặc theo từ
        if (norm == "os" || norm == "he dieu hanh" || norm == "operating system" || words.Contains("os"))
            return nameof(VirtualMachineEntry.Os);

        // USER / USERNAME / TÀI KHOẢN
        if (norm == "user" || norm == "username" || norm == "tai khoan" || norm == "nguoi dung" || words.Contains("user"))
            return nameof(VirtualMachineEntry.Username);

        // PASSWORD / P: phải là 'p' chính xác hoặc từ pass/password (tránh bắt nhầm chữ cái 'p' trong Description, Dept, Group)
        if (norm == "p" || norm == "pass" || norm == "password" || norm == "mat khau" ||
            words.Contains("pass") || words.Contains("password"))
            return nameof(VirtualMachineEntry.Password);

        // TNS: so khớp chính xác hoặc từ
        if (norm == "tns" || norm == "tns name" || words.Contains("tns"))
            return nameof(VirtualMachineEntry.Tns);

        // DATABASE NAME: tránh nhầm với Database Oracle Version
        if (norm == "database name" || norm == "db name" || norm == "ten db" || norm == "database" || norm == "db" || norm == "dbname" ||
            norm.StartsWith("database name") || norm.StartsWith("db name"))
            return nameof(VirtualMachineEntry.DatabaseName);

        // ORACLE VERSION
        if (norm.Contains("version") || norm.Contains("phien ban oracle") || norm.Contains("version oracl"))
            return nameof(VirtualMachineEntry.OracleVersion);

        // NGƯỜI KIỂM TRA
        if (norm.Contains("nguoi kiem") || norm.Contains("tester") || norm.Contains("quan tri") || norm.Contains("nguoi kt"))
            return nameof(VirtualMachineEntry.Tester);

        // GHI CHÚ
        if (norm.Contains("ghi chu") || norm == "note" || norm == "notes" || norm == "mo ta")
            return nameof(VirtualMachineEntry.Notes);

        // NGÀY HẾT HẠN
        if (norm.Contains("het han") || norm.Contains("expire") || norm.Contains("expiration"))
            return nameof(VirtualMachineEntry.ExpirationDate);

        return null;
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
        return patterns.Any(p => text == p || text.StartsWith(p));
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
                    // Luôn ghi đè số thứ tự từ file Excel
                    existing.No = string.IsNullOrWhiteSpace(incoming.No) ? existing.No : incoming.No;
                    if (!string.IsNullOrWhiteSpace(incoming.No))
                    {
                        existing.No = incoming.No;
                    }
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
