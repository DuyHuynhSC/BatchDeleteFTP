using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
            EnsureProfilesInitialized(defaultSettings);
            Save(defaultSettings, targetPath);
            return defaultSettings;
        }

        string json = File.ReadAllText(targetPath);
        var settings = JsonSerializer.Deserialize<FtpSettings>(json, JsonOptions) ?? new FtpSettings();
        EnsureProfilesInitialized(settings);
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

        UpsertProfile(settings, settings.SelectedProfileName);

        string json = JsonSerializer.Serialize(settings, JsonOptions);
        File.WriteAllText(targetPath, json);
    }

    public void EnsureProfilesInitialized(FtpSettings settings)
    {
        if (settings.RemotePaths == null || settings.RemotePaths.Count == 0)
        {
            settings.RemotePaths = new List<string> { "/" };
        }

        settings.Profiles ??= new List<FtpSiteProfile>();

        if (settings.Profiles.Count == 0)
        {
            string defaultName = !string.IsNullOrWhiteSpace(settings.SelectedProfileName)
                ? settings.SelectedProfileName.Trim()
                : (!string.IsNullOrWhiteSpace(settings.Username) ? settings.Username.Trim() : "Default");

            settings.SelectedProfileName = defaultName;
            settings.Profiles.Add(FtpSiteProfile.FromSettings(defaultName, settings));
        }
        else if (string.IsNullOrWhiteSpace(settings.SelectedProfileName))
        {
            settings.SelectedProfileName = settings.Profiles[0].Name;
        }
    }

    public FtpSiteProfile UpsertProfile(FtpSettings settings, string profileName)
    {
        settings.Profiles ??= new List<FtpSiteProfile>();
        string cleanName = string.IsNullOrWhiteSpace(profileName) ? "Default" : profileName.Trim();
        settings.SelectedProfileName = cleanName;

        var updated = FtpSiteProfile.FromSettings(cleanName, settings);
        int idx = settings.Profiles.FindIndex(p => string.Equals(p.Name, cleanName, StringComparison.OrdinalIgnoreCase));
        if (idx >= 0)
        {
            settings.Profiles[idx] = updated;
        }
        else
        {
            settings.Profiles.Add(updated);
        }

        return updated;
    }

    public bool SelectProfile(FtpSettings settings, string profileName)
    {
        if (settings.Profiles == null || string.IsNullOrWhiteSpace(profileName))
        {
            return false;
        }

        var found = settings.Profiles.FirstOrDefault(p =>
            string.Equals(p.Name, profileName.Trim(), StringComparison.OrdinalIgnoreCase));
        if (found == null)
        {
            return false;
        }

        found.ApplyTo(settings);
        return true;
    }

    public bool DeleteProfile(FtpSettings settings, string profileName)
    {
        if (settings.Profiles == null || string.IsNullOrWhiteSpace(profileName))
        {
            return false;
        }

        int removed = settings.Profiles.RemoveAll(p =>
            string.Equals(p.Name, profileName.Trim(), StringComparison.OrdinalIgnoreCase));
        if (removed == 0)
        {
            return false;
        }

        if (settings.Profiles.Count == 0)
        {
            var fallback = new FtpSettings();
            var defaultProfile = FtpSiteProfile.FromSettings("Default", fallback);
            settings.Profiles.Add(defaultProfile);
        }

        settings.Profiles[0].ApplyTo(settings);
        return true;
    }
}
