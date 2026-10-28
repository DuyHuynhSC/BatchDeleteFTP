using System;
using System.IO;
using System.Text.Json;
using BatchDeleteFTP.Models;

namespace BatchDeleteFTP.Services;

public class ConfigService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public string DefaultConfigPath => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.json");

    public FtpSettings Load(string? filePath = null)
    {
        string targetPath = filePath ?? DefaultConfigPath;
        if (!File.Exists(targetPath))
        {
            var defaultSettings = new FtpSettings();
            Save(defaultSettings, targetPath);
            return defaultSettings;
        }

        string json = File.ReadAllText(targetPath);
        var settings = JsonSerializer.Deserialize<FtpSettings>(json, JsonOptions) ?? new FtpSettings();
        if (settings.RemotePaths == null || settings.RemotePaths.Count == 0)
        {
            settings.RemotePaths = new() { "/" };
        }
        return settings;
    }

    public void Save(FtpSettings settings, string? filePath = null)
    {
        string targetPath = filePath ?? DefaultConfigPath;
        string? dir = Path.GetDirectoryName(targetPath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        string json = JsonSerializer.Serialize(settings, JsonOptions);
        File.WriteAllText(targetPath, json);
    }
}
