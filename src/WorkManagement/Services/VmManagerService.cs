using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Encodings.Web;
using System.Text.Json;
using WorkManagement.Models;

namespace WorkManagement.Services;

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
            // Trả về danh sách rỗng nếu file bị lỗi hoặc không thể đọc
        }

        return new List<VirtualMachineEntry>();
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
}
