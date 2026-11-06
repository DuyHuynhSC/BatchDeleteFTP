using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using MiniExcelLibs;
using WorkManagement.Models;

namespace WorkManagement.Services;

public class ProjectExcelService
{
    public Task<List<ProjectEntry>> ReadProjectsFromExcelAsync(string filePath)
    {
        return Task.Run(() =>
        {
            var result = new List<ProjectEntry>();
            if (!File.Exists(filePath)) return result;

            using var stream = File.OpenRead(filePath);
            var rows = stream.Query(useHeaderRow: true).ToList();

            foreach (IDictionary<string, object> row in rows)
            {
                if (row == null || row.Count == 0) continue;

                string GetVal(params string[] columnKeys)
                {
                    foreach (var key in columnKeys)
                    {
                        var match = row.FirstOrDefault(kvp =>
                            kvp.Key != null &&
                            kvp.Key.Trim().Equals(key, StringComparison.OrdinalIgnoreCase));
                        if (match.Key != null && match.Value != null)
                        {
                            string s = match.Value.ToString()?.Trim() ?? string.Empty;
                            if (!string.IsNullOrEmpty(s)) return s;
                        }
                    }

                    // Fallback to partial match
                    foreach (var key in columnKeys)
                    {
                        var match = row.FirstOrDefault(kvp =>
                            kvp.Key != null &&
                            kvp.Key.Contains(key, StringComparison.OrdinalIgnoreCase));
                        if (match.Key != null && match.Value != null)
                        {
                            string s = match.Value.ToString()?.Trim() ?? string.Empty;
                            if (!string.IsNullOrEmpty(s)) return s;
                        }
                    }

                    return string.Empty;
                }

                string projectName = GetVal("Project", "Tên Dự Án", "TenDuAn", "ProjectName");
                string sourceFolder = GetVal("Source Folder", "SourceFolder", "Thư mục source", "Folder");
                string host = GetVal("Host Server", "HostServer", "Host", "IP", "Máy ảo");

                // Bỏ qua dòng trống nếu không có thông tin dự án tối thiểu
                if (string.IsNullOrWhiteSpace(projectName) && string.IsNullOrWhiteSpace(sourceFolder) && string.IsNullOrWhiteSpace(host))
                {
                    continue;
                }

                string no = GetVal("No", "STT", "Mã");
                string desc = GetVal("Giải thích thêm (Version - Mục đích)", "Giải thích thêm", "Description", "Mô tả", "Version");
                string tns = GetVal("tnsname (tham khảo file:", "tnsname", "TNS", "TnsName");
                string service = GetVal("Service Name", "ServiceName", "Service", "SID", "Database Name");
                if (string.IsNullOrWhiteSpace(service)) service = "ORCL";

                string port = GetVal("Port", "Cổng");
                if (string.IsNullOrWhiteSpace(port)) port = "1521";

                string user = GetVal("User", "DB User", "DB Users", "Người dùng");
                string tablespace = GetVal("Tablespace", "Tên Tablespace", "Tablespace Name");
                string dbaUser = GetVal("DBA User", "Tài khoản DBA", "Admin User");
                if (string.IsNullOrWhiteSpace(dbaUser)) dbaUser = "SYSTEM";

                string dbaPass = GetVal("DBA Password", "Mật khẩu DBA", "DBA Pass");
                string notes = GetVal("Ghi chú", "Notes", "GhiChu");
                string person = GetVal("Người phụ trách", "Person In Charge", "Tester", "Người KT", "PIC");
                string notes2 = GetVal("Ghi chú 2", "Notes 2", "GhiChu2");

                var entry = new ProjectEntry
                {
                    No = no,
                    ProjectName = projectName,
                    Description = desc,
                    SourceFolder = sourceFolder,
                    TnsName = tns,
                    HostServer = host,
                    ServiceName = service,
                    Port = port,
                    DbUsers = user,
                    Tablespace = tablespace,
                    DbaUsername = dbaUser,
                    DbaPassword = dbaPass,
                    Notes = notes,
                    PersonInCharge = person,
                    Notes2 = notes2
                };

                result.Add(entry);
            }

            return result;
        });
    }

    public Task ExportTemplateAsync(string destinationPath)
    {
        return Task.Run(() =>
        {
            var sampleRows = new List<Dictionary<string, object>>
            {
                new()
                {
                    ["No"] = "316",
                    ["Project"] = "UTSUGI",
                    ["Giải thích thêm (Version - Mục đích)"] = "Customize V10",
                    ["Source Folder"] = @"Q:\V10_UTG",
                    ["tnsname (tham khảo file:"] = "ORCL19F",
                    ["Host Server"] = "172.16.6.23",
                    ["Service Name"] = "ORCL",
                    ["Port"] = "1521",
                    ["User"] = "UTG_RPS, UTG_USR1, UTG_USR9, UTG_LOG",
                    ["Tablespace"] = "TS_UTSUGI",
                    ["DBA User"] = "SYSTEM",
                    ["Ghi chú"] = "2026/06/23 Tạo mới",
                    ["Người phụ trách"] = "Duy",
                    ["Ghi chú 2"] = ""
                },
                new()
                {
                    ["No"] = "324",
                    ["Project"] = "CUORE",
                    ["Giải thích thêm (Version - Mục đích)"] = "Customize V10",
                    ["Source Folder"] = @"Q:\V10_CUORE",
                    ["tnsname (tham khảo file:"] = "ORCL19F",
                    ["Host Server"] = "172.16.6.23",
                    ["Service Name"] = "ORCL",
                    ["Port"] = "1521",
                    ["User"] = "COR_RPS, COR_USR1, COR_USR9, COR_LOG, COR_TST_USR1(Dùng để test script)",
                    ["Tablespace"] = "TS_CUORE",
                    ["DBA User"] = "SYSTEM",
                    ["Ghi chú"] = "2026/07/14 Tạo mới",
                    ["Người phụ trách"] = "Duy",
                    ["Ghi chú 2"] = ""
                }
            };

            MiniExcel.SaveAs(destinationPath, sampleRows, overwriteFile: true);
        });
    }
}
