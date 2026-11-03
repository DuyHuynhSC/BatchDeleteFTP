using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using BatchDeleteFTP.Models;
using MaterialDesignThemes.Wpf;

namespace BatchDeleteFTP.Services;

public static class ThemeService
{
    private const string ThemeDictionaryPrefix = "/BatchDeleteFTP;component/Themes/";

    public static IReadOnlyList<AppThemeOption> AvailableThemes { get; } = new List<AppThemeOption>
    {
        new(
            AppThemeKind.MaterialLight,
            nameof(AppThemeKind.MaterialLight),
            "Material Light",
            "Giao diện phẳng Google Material sáng sủa, hiện đại",
            "WeatherSunny",
            IsDarkBase: false),
        new(
            AppThemeKind.MaterialDark,
            nameof(AppThemeKind.MaterialDark),
            "Material Dark",
            "Giao diện tối dịu mắt, độ tương phản cao ban đêm",
            "WeatherNight",
            IsDarkBase: true),
        new(
            AppThemeKind.Skeuomorphism,
            nameof(AppThemeKind.Skeuomorphism),
            "Skeuomorphism",
            "Mô phỏng chất liệu kim loại phay, viền nổi 3D & màn hình CRT cổ điển",
            "TextureBox",
            IsDarkBase: false),
        new(
            AppThemeKind.Neumorphism,
            nameof(AppThemeKind.Neumorphism),
            "Neumorphism",
            "Soft UI đúc nổi liền khối, bóng đổ kép mềm mại tinh tế",
            "LayersTripleOutline",
            IsDarkBase: false),
        new(
            AppThemeKind.FrutigerAero,
            nameof(AppThemeKind.FrutigerAero),
            "Frutiger Aero",
            "Kính trong suốt Aero Glass, nút bấm Glossy Gel & sắc xanh đại dương",
            "WaterOutline",
            IsDarkBase: false)
    };

    public static AppThemeOption GetThemeOption(AppThemeKind kind) =>
        AvailableThemes.FirstOrDefault(t => t.Kind == kind) ?? AvailableThemes[0];

    public static AppThemeKind ParseTheme(string? themeName, bool fallbackIsDark = false)
    {
        if (!string.IsNullOrWhiteSpace(themeName))
        {
            string normalized = themeName.Trim().Replace(" ", string.Empty).Replace("-", string.Empty).Replace("_", string.Empty);

            if (Enum.TryParse<AppThemeKind>(normalized, ignoreCase: true, out var parsed))
            {
                return parsed;
            }

            if (string.Equals(normalized, "Dark", StringComparison.OrdinalIgnoreCase))
            {
                return AppThemeKind.MaterialDark;
            }

            if (string.Equals(normalized, "Light", StringComparison.OrdinalIgnoreCase))
            {
                return AppThemeKind.MaterialLight;
            }
        }

        return fallbackIsDark ? AppThemeKind.MaterialDark : AppThemeKind.MaterialLight;
    }

    public static string GetThemeDictionaryUri(AppThemeKind kind) => kind switch
    {
        AppThemeKind.MaterialDark => "pack://application:,,,/BatchDeleteFTP;component/Themes/MaterialDarkTheme.xaml",
        AppThemeKind.Skeuomorphism => "pack://application:,,,/BatchDeleteFTP;component/Themes/SkeuomorphismTheme.xaml",
        AppThemeKind.Neumorphism => "pack://application:,,,/BatchDeleteFTP;component/Themes/NeumorphismTheme.xaml",
        AppThemeKind.FrutigerAero => "pack://application:,,,/BatchDeleteFTP;component/Themes/FrutigerAeroTheme.xaml",
        _ => "pack://application:,,,/BatchDeleteFTP;component/Themes/MaterialLightTheme.xaml"
    };

    public static void ApplyTheme(AppThemeKind kind)
    {
        var paletteHelper = new PaletteHelper();
        Theme materialTheme = paletteHelper.GetTheme();

        switch (kind)
        {
            case AppThemeKind.MaterialDark:
                materialTheme.SetBaseTheme(BaseTheme.Dark);
                materialTheme.SetPrimaryColor(Color.FromRgb(0x42, 0xA5, 0xF5));
                materialTheme.SetSecondaryColor(Color.FromRgb(0x26, 0xA6, 0x9A));
                break;

            case AppThemeKind.Skeuomorphism:
                materialTheme.SetBaseTheme(BaseTheme.Light);
                materialTheme.SetPrimaryColor(Color.FromRgb(0x23, 0x53, 0x8F));
                materialTheme.SetSecondaryColor(Color.FromRgb(0x8C, 0x58, 0x28));
                break;

            case AppThemeKind.Neumorphism:
                materialTheme.SetBaseTheme(BaseTheme.Light);
                materialTheme.SetPrimaryColor(Color.FromRgb(0x4A, 0x69, 0xBD));
                materialTheme.SetSecondaryColor(Color.FromRgb(0x20, 0xC9, 0x97));
                break;

            case AppThemeKind.FrutigerAero:
                materialTheme.SetBaseTheme(BaseTheme.Light);
                materialTheme.SetPrimaryColor(Color.FromRgb(0x00, 0x78, 0xD4));
                materialTheme.SetSecondaryColor(Color.FromRgb(0x00, 0xBF, 0xA5));
                break;

            case AppThemeKind.MaterialLight:
            default:
                materialTheme.SetBaseTheme(BaseTheme.Light);
                materialTheme.SetPrimaryColor(Color.FromRgb(0x39, 0x49, 0xAB));
                materialTheme.SetSecondaryColor(Color.FromRgb(0x00, 0x89, 0x7B));
                break;
        }

        paletteHelper.SetTheme(materialTheme);

        if (Application.Current == null)
        {
            return;
        }

        var merged = Application.Current.Resources.MergedDictionaries;
        var newThemeDict = new ResourceDictionary
        {
            Source = new Uri(GetThemeDictionaryUri(kind), UriKind.Absolute)
        };

        var existingThemeDicts = merged
            .Where(d => d.Source != null &&
                        d.Source.OriginalString.Contains("Themes/", StringComparison.OrdinalIgnoreCase) &&
                        d.Source.OriginalString.EndsWith("Theme.xaml", StringComparison.OrdinalIgnoreCase))
            .ToList();

        foreach (var oldDict in existingThemeDicts)
        {
            merged.Remove(oldDict);
        }

        // Insert theme dictionary before ThemeTokens.xaml if present, or add to the end
        int tokensIdx = -1;
        for (int i = 0; i < merged.Count; i++)
        {
            if (merged[i].Source != null &&
                merged[i].Source.OriginalString.EndsWith("ThemeTokens.xaml", StringComparison.OrdinalIgnoreCase))
            {
                tokensIdx = i;
                break;
            }
        }

        if (tokensIdx >= 0)
        {
            merged.Insert(tokensIdx, newThemeDict);
        }
        else
        {
            merged.Add(newThemeDict);
        }

        // Copy dynamic resources directly into Application.Current.Resources so DynamicResource lookups resolve immediately
        foreach (object key in newThemeDict.Keys)
        {
            Application.Current.Resources[key] = newThemeDict[key];
        }
    }
}
