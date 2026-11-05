using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Encodings.Web;
using System.Text.Json;
using BatchDeleteFTP.Models;

namespace BatchDeleteFTP.Services;

public class VmManagerService
{
    private readonly string _storagePath;
    private readonly JsonSerializerOptions _jsonOptions;

    public VmManagerService(string? customStoragePath = null)
    {
        if (!string.IsNullOrWhiteSpace(customStoragePath))
        {
            _storagePath = customStoragePath;
        }
        else
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string appDir = Path.Combine(appData, "WorkManagement");
            Directory.CreateDirectory(appDir);
            _storagePath = Path.Combine(appDir, "virtual_machines.json");
        }

        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };
    }

    public List<VirtualMachineEntry> LoadAll()
    {
        try
        {
            if (File.Exists(_storagePath))
            {
                string json = File.ReadAllText(_storagePath);
                var list = JsonSerializer.Deserialize<List<VirtualMachineEntry>>(json, _jsonOptions);
                if (list != null)
                {
                    return list;
                }
            }
        }
        catch
        {
            // Fallback to default initial list if corrupted or empty
        }

        var initialList = GetInitialSampleData();
        SaveAll(initialList);
        return initialList;
    }

    public void ClearAll()
    {
        SaveAll(new List<VirtualMachineEntry>());
    }

    public void SaveAll(IEnumerable<VirtualMachineEntry> list)
    {
        try
        {
            string? dir = Path.GetDirectoryName(_storagePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            string json = JsonSerializer.Serialize(list, _jsonOptions);
            File.WriteAllText(_storagePath, json);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Không thể lưu dữ liệu máy ảo: {ex.Message}", ex);
        }
    }

    public static List<VirtualMachineEntry> GetInitialSampleData()
    {
        return new List<VirtualMachineEntry>
        {
            new()
            {
                No = "17",
                Ip = "172.16.6.16",
                MachineName = "D2-SVR15-6016",
                DomainOrWorkgroup = "FUJINET.vn",
                Os = "Windows Server 2008 64bit",
                Username = "Administrator",
                Password = "D2-20100802",
                Tns = "ORCL11GR2K",
                DatabaseName = "ORCL11GR2K",
                OracleVersion = "11.2.0.4.0",
                Tester = "Duy",
                Notes = "Oracle Server 11gr2",
                ExpirationDate = "Vô hạn"
            },
            new()
            {
                No = "18",
                Ip = "172.16.6.24",
                MachineName = "D2-Svr10-6024",
                DomainOrWorkgroup = "FUJINET.vn",
                Os = "Windows Server 2012 R2 64 bit",
                Username = "Administrator",
                Password = "D2-20100802",
                Tns = "ORCL12E",
                DatabaseName = "ORCL12E",
                OracleVersion = "12.1.0.1.0",
                Tester = "Duy",
                Notes = "Oracle Server 12c",
                ExpirationDate = "Vô hạn"
            },
            new()
            {
                No = "30",
                Ip = "172.16.6.152",
                MachineName = "DEPT2-SVR6152",
                DomainOrWorkgroup = "WORKGROUP",
                Os = "Windows Server 2012 R2 64 bit",
                Username = "Administrator",
                Password = "D2-20100802",
                Tns = "ORCL12G",
                DatabaseName = "ORCL12G",
                OracleVersion = "12.2.0.1.0",
                Tester = "Duy",
                Notes = "Oracle Server 12c (12.2)",
                ExpirationDate = "2020/12/31"
            },
            new()
            {
                No = "50",
                Ip = "172.16.7.60",
                MachineName = "DEPT2-SVR-5060",
                DomainOrWorkgroup = "FUJINET.vn",
                Os = "Windows Server 2008 R2 64bit",
                Username = "Administrator",
                Password = "Fujinet02",
                Tns = "ORCL11GR2J",
                DatabaseName = "ORCL11GR2J",
                OracleVersion = "11.2.0.2.0",
                Tester = "Duy",
                Notes = "Oracle Server 11gr2",
                ExpirationDate = "Vô hạn"
            },
            new()
            {
                No = "52",
                Ip = "172.16.6.174",
                MachineName = "DEPT2-SVR-6174",
                DomainOrWorkgroup = "FUJINET.vn",
                Os = "Windows Server 2016 Standard 64bit",
                Username = "Administrator",
                Password = "Fujinet02",
                Tns = "ORCL12O",
                DatabaseName = "ORCL12O",
                OracleVersion = "12.2.0.1.0",
                Tester = "Duy",
                Notes = "Oracle Server 12c",
                ExpirationDate = "Vô hạn"
            },
            new()
            {
                No = "59",
                Ip = "172.16.7.93",
                MachineName = "DEPT2-VM-7093",
                DomainOrWorkgroup = "FUJINET.vn",
                Os = "Windows Server 2019 Standard 64bit",
                Username = "Administrator",
                Password = "Fujinet02",
                Tns = "ORCL",
                DatabaseName = "ORCL19A",
                OracleVersion = "19.3.0.0.0",
                Tester = "Duy",
                Notes = "Oracle Server 19c",
                ExpirationDate = "2020/12/31"
            },
            new()
            {
                No = "76",
                Ip = "172.16.6.116",
                MachineName = "D2-VM-6116",
                DomainOrWorkgroup = "FUJINET.vn",
                Os = "Windows Server 2019 Standard 64bit",
                Username = "User01",
                Password = "Abc12345",
                Tns = "",
                DatabaseName = "",
                OracleVersion = "",
                Tester = "Duy",
                Notes = "SQL Server 2019",
                ExpirationDate = "2026/12/30"
            },
            new()
            {
                No = "76",
                Ip = "172.16.6.23",
                MachineName = "D2-VM-7023",
                DomainOrWorkgroup = "FUJINET.vn",
                Os = "Windows Server 2022 Standard 64bit",
                Username = "User01",
                Password = "Dept2123",
                Tns = "ORCL19C",
                DatabaseName = "ORCL",
                OracleVersion = "19.15",
                Tester = "Duy",
                Notes = "Oracle Server 19c (19.15)",
                ExpirationDate = "2026/12/31"
            }
        };
    }
}
