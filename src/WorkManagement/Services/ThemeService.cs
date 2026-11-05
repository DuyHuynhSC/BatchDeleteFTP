using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using WorkManagement.Models;
using MaterialDesignThemes.Wpf;

namespace WorkManagement.Services;

public static class ThemeService
{
    private const string ThemeDictionaryPrefix = "/WorkManagement;component/Themes/";

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
            IsDarkBase: false),
        new(
            AppThemeKind.Claymorphism,
            nameof(AppThemeKind.Claymorphism),
            "Claymorphism",
            "Đất sét 3D phồng mềm, góc bo tròn lớn & bóng đổ marshmallow sâu",
            "Shapes",
            IsDarkBase: false),
        new(
            AppThemeKind.Glassmorphism,
            nameof(AppThemeKind.Glassmorphism),
            "Glassmorphism",
            "Kính mờ bán trong suốt đa tầng, phản quang trên nền vũ trụ sâu",
            "Blur",
            IsDarkBase: true),
        new(
            AppThemeKind.Y2KDigital,
            nameof(AppThemeKind.Y2KDigital),
            "Y2K Digital",
            "Cyberpunk thập niên 2000, ánh kim loại bạc, viền Neon Cyan & Magenta",
            "CreationOutline",
            IsDarkBase: true)
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

            if (string.Equals(normalized, "Clay", StringComparison.OrdinalIgnoreCase))
            {
                return AppThemeKind.Claymorphism;
            }

            if (string.Equals(normalized, "Glass", StringComparison.OrdinalIgnoreCase))
            {
                return AppThemeKind.Glassmorphism;
            }

            if (string.Equals(normalized, "Y2K", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(normalized, "Y2KDigitalAesthetic", StringComparison.OrdinalIgnoreCase))
            {
                return AppThemeKind.Y2KDigital;
            }
        }

        return fallbackIsDark ? AppThemeKind.MaterialDark : AppThemeKind.MaterialLight;
    }

    public static string GetThemeDictionaryUri(AppThemeKind kind) => kind switch
    {
        AppThemeKind.MaterialDark => "pack://application:,,,/WorkManagement;component/Themes/MaterialDarkTheme.xaml",
        AppThemeKind.Skeuomorphism => "pack://application:,,,/WorkManagement;component/Themes/SkeuomorphismTheme.xaml",
        AppThemeKind.Neumorphism => "pack://application:,,,/WorkManagement;component/Themes/NeumorphismTheme.xaml",
        AppThemeKind.FrutigerAero => "pack://application:,,,/WorkManagement;component/Themes/FrutigerAeroTheme.xaml",
        AppThemeKind.Claymorphism => "pack://application:,,,/WorkManagement;component/Themes/ClaymorphismTheme.xaml",
        AppThemeKind.Glassmorphism => "pack://application:,,,/WorkManagement;component/Themes/GlassmorphismTheme.xaml",
        AppThemeKind.Y2KDigital => "pack://application:,,,/WorkManagement;component/Themes/Y2KDigitalTheme.xaml",
        _ => "pack://application:,,,/WorkManagement;component/Themes/MaterialLightTheme.xaml"
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

            case AppThemeKind.Claymorphism:
                materialTheme.SetBaseTheme(BaseTheme.Light);
                materialTheme.SetPrimaryColor(Color.FromRgb(0x5A, 0x67, 0xD8));
                materialTheme.SetSecondaryColor(Color.FromRgb(0x38, 0xB2, 0xAC));
                break;

            case AppThemeKind.Glassmorphism:
                materialTheme.SetBaseTheme(BaseTheme.Dark);
                materialTheme.SetPrimaryColor(Color.FromRgb(0x38, 0xBD, 0xF8));
                materialTheme.SetSecondaryColor(Color.FromRgb(0x2D, 0xD4, 0xBF));
                break;

            case AppThemeKind.Y2KDigital:
                materialTheme.SetBaseTheme(BaseTheme.Dark);
                materialTheme.SetPrimaryColor(Color.FromRgb(0x00, 0xF0, 0xFF));
                materialTheme.SetSecondaryColor(Color.FromRgb(0xFF, 0x00, 0x7F));
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
