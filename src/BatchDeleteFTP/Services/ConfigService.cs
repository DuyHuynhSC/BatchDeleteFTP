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
        SyncEmailSettingsAcrossAllProfiles(settings);
        NormalizeThemeSettings(settings);

        string json = JsonSerializer.Serialize(settings, JsonOptions);
        File.WriteAllText(targetPath, json);
    }

    public void EnsureProfilesInitialized(FtpSettings settings)
    {
        NormalizeThemeSettings(settings);

        if (settings.RemotePaths == null || settings.RemotePaths.Count == 0)
        {
            settings.RemotePaths = new List<string> { "/" };
        }

        settings.Profiles ??= new List<FtpSiteProfile>();

        // Backward compatibility: If root email settings are empty but an existing profile has email config, adopt it
        if ((settings.RecipientEmails == null || settings.RecipientEmails.Count == 0) &&
            string.IsNullOrWhiteSpace(settings.SenderEmail) &&
            string.IsNullOrWhiteSpace(settings.SmtpUser))
        {
            var profWithEmail = settings.Profiles.FirstOrDefault(p =>
                (p.RecipientEmails != null && p.RecipientEmails.Count > 0) ||
                !string.IsNullOrWhiteSpace(p.SenderEmail) ||
                !string.IsNullOrWhiteSpace(p.SmtpUser));

            if (profWithEmail != null)
            {
                settings.SmtpHost = profWithEmail.SmtpHost;
                settings.SmtpPort = profWithEmail.SmtpPort;
                settings.SmtpEnableSsl = profWithEmail.SmtpEnableSsl;
                settings.SmtpUser = profWithEmail.SmtpUser;
                settings.SmtpPassword = profWithEmail.SmtpPassword;
                settings.SenderEmail = profWithEmail.SenderEmail;
                settings.RecipientEmails = new List<string>(profWithEmail.RecipientEmails ?? new List<string>());
                settings.AutoSendEmailOnPreview = profWithEmail.AutoSendEmailOnPreview;
            }
        }

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

        settings.SelectedScanProfileNames ??= new List<string>();
        var validProfileNames = new HashSet<string>(settings.Profiles.Select(p => p.Name), StringComparer.OrdinalIgnoreCase);
        settings.SelectedScanProfileNames = settings.SelectedScanProfileNames
            .Where(name => !string.IsNullOrWhiteSpace(name) && validProfileNames.Contains(name.Trim()))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (settings.SelectedScanProfileNames.Count == 0 && !string.IsNullOrWhiteSpace(settings.SelectedProfileName))
        {
            settings.SelectedScanProfileNames.Add(settings.SelectedProfileName);
        }

        SyncEmailSettingsAcrossAllProfiles(settings);
    }

    public static void SyncEmailSettingsAcrossAllProfiles(FtpSettings settings)
    {
        if (settings.Profiles == null) return;
        foreach (var profile in settings.Profiles)
        {
            profile.SyncEmailSettings(settings);
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

        SyncEmailSettingsAcrossAllProfiles(settings);
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
            fallback.SmtpHost = settings.SmtpHost;
            fallback.SmtpPort = settings.SmtpPort;
            fallback.SmtpEnableSsl = settings.SmtpEnableSsl;
            fallback.SmtpUser = settings.SmtpUser;
            fallback.SmtpPassword = settings.SmtpPassword;
            fallback.SenderEmail = settings.SenderEmail;
            fallback.RecipientEmails = new List<string>(settings.RecipientEmails ?? new List<string>());
            fallback.AutoSendEmailOnPreview = settings.AutoSendEmailOnPreview;

            var defaultProfile = FtpSiteProfile.FromSettings("Default", fallback);
            settings.Profiles.Add(defaultProfile);
        }

        settings.Profiles[0].ApplyTo(settings);

        if (settings.SelectedScanProfileNames != null)
        {
            settings.SelectedScanProfileNames.RemoveAll(p => string.Equals(p, profileName.Trim(), StringComparison.OrdinalIgnoreCase));
            if (settings.SelectedScanProfileNames.Count == 0 && !string.IsNullOrWhiteSpace(settings.SelectedProfileName))
            {
                settings.SelectedScanProfileNames.Add(settings.SelectedProfileName);
            }
        }

        return true;
    }

    public static void NormalizeThemeSettings(FtpSettings settings)
    {
        if (string.IsNullOrWhiteSpace(settings.ThemeName))
        {
            settings.ThemeName = settings.IsDarkTheme
                ? AppThemeKind.MaterialDark.ToString()
                : AppThemeKind.MaterialLight.ToString();
        }
        else
        {
            var resolved = ThemeService.ParseTheme(settings.ThemeName, settings.IsDarkTheme);
            settings.ThemeName = resolved.ToString();
            settings.IsDarkTheme = ThemeService.GetThemeOption(resolved).IsDarkBase;
        }
    }
}
