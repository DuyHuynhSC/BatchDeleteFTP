using System;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Media;
using BatchDeleteFTP.Models;
using BatchDeleteFTP.Services;
using Xunit;

namespace BatchDeleteFTP.Tests;

public class ThemeServiceTests
{
    [Theory]
    [InlineData("MaterialLight", false, AppThemeKind.MaterialLight)]
    [InlineData("MaterialDark", false, AppThemeKind.MaterialDark)]
    [InlineData("Skeuomorphism", false, AppThemeKind.Skeuomorphism)]
    [InlineData("Neumorphism", false, AppThemeKind.Neumorphism)]
    [InlineData("FrutigerAero", false, AppThemeKind.FrutigerAero)]
    [InlineData("Frutiger Aero", false, AppThemeKind.FrutigerAero)]
    [InlineData("Dark", false, AppThemeKind.MaterialDark)]
    [InlineData("Light", true, AppThemeKind.MaterialLight)]
    [InlineData("", true, AppThemeKind.MaterialDark)]
    [InlineData(null, false, AppThemeKind.MaterialLight)]
    [InlineData("InvalidThemeName", false, AppThemeKind.MaterialLight)]
    public void ParseTheme_ResolvesExpectedThemeKind(string? input, bool fallbackDark, AppThemeKind expected)
    {
        var result = ThemeService.ParseTheme(input, fallbackDark);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void ConfigService_PreservesThemeName_AndBackwardCompatibilityWithIsDarkTheme()
    {
        string tempFile = Path.Combine(Path.GetTempPath(), $"ftp_theme_{Guid.NewGuid()}.json");
        try
        {
            var service = new ConfigService();

            // 1. Legacy config with only IsDarkTheme = true -> resolves to MaterialDark
            var legacyDark = new FtpSettings
            {
                IsDarkTheme = true,
                ThemeName = string.Empty
            };
            service.Save(legacyDark, tempFile);
            var loadedLegacy = service.Load(tempFile);
            Assert.Equal("MaterialDark", loadedLegacy.ThemeName);
            Assert.True(loadedLegacy.IsDarkTheme);

            // 2. Explicit FrutigerAero theme -> preserves FrutigerAero
            loadedLegacy.ThemeName = nameof(AppThemeKind.FrutigerAero);
            service.Save(loadedLegacy, tempFile);
            var loadedAero = service.Load(tempFile);
            Assert.Equal("FrutigerAero", loadedAero.ThemeName);
            Assert.False(loadedAero.IsDarkTheme);
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    [Fact]
    public void ApplyTheme_SwitchesAllFiveThemesAndUpdatesDynamicResources()
    {
        Exception? threadEx = null;
        var thread = new Thread(() =>
        {
            try
            {
                if (Application.Current == null)
                {
                    var app = new App();
                    app.InitializeComponent();
                }

                var window = new MainWindow();

                foreach (var option in ThemeService.AvailableThemes)
                {
                    window.SetTheme(option.Kind);

                    var windowBg = Application.Current!.TryFindResource("AppWindowBackgroundBrush") as Brush;
                    var cardBg = Application.Current!.TryFindResource("AppCardBackgroundBrush") as Brush;
                    var headerBg = Application.Current!.TryFindResource("AppHeaderBackgroundBrush") as Brush;
                    var primaryBtnBg = Application.Current!.TryFindResource("AppPrimaryButtonBackground") as Brush;

                    Assert.NotNull(windowBg);
                    Assert.NotNull(cardBg);
                    Assert.NotNull(headerBg);
                    Assert.NotNull(primaryBtnBg);
                }

                // Verify Skeuomorphism uses LinearGradientBrush for Window Background and CRT green for Terminal Foreground
                window.SetTheme(AppThemeKind.Skeuomorphism);
                Assert.IsType<LinearGradientBrush>(Application.Current!.TryFindResource("AppWindowBackgroundBrush"));
                var skeuoTermFg = Application.Current!.TryFindResource("AppTerminalForegroundBrush") as SolidColorBrush;
                Assert.NotNull(skeuoTermFg);
                Assert.Equal(Color.FromRgb(0x66, 0xFF, 0x99), skeuoTermFg!.Color);

                // Verify Neumorphism uses #E0E5EC Cool-Grey Window Background
                window.SetTheme(AppThemeKind.Neumorphism);
                var neumoBg = Application.Current!.TryFindResource("AppWindowBackgroundBrush") as SolidColorBrush;
                Assert.NotNull(neumoBg);
                Assert.Equal(Color.FromRgb(0xE0, 0xE5, 0xEC), neumoBg!.Color);

                // Verify Frutiger Aero uses LinearGradientBrush for Window & Glossy Button
                window.SetTheme(AppThemeKind.FrutigerAero);
                Assert.IsType<LinearGradientBrush>(Application.Current!.TryFindResource("AppWindowBackgroundBrush"));
                Assert.IsType<LinearGradientBrush>(Application.Current!.TryFindResource("AppPrimaryButtonBackground"));
            }
            catch (Exception ex)
            {
                threadEx = ex;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        Assert.Null(threadEx);
    }
}
