using System;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Media;
using BatchDeleteFTP.Models;
using BatchDeleteFTP.Services;
using Xunit;

namespace BatchDeleteFTP.Tests;

[Collection("WpfApp")]
public class ThemeServiceTests
{
    [Theory]
    [InlineData("MaterialLight", false, AppThemeKind.MaterialLight)]
    [InlineData("MaterialDark", false, AppThemeKind.MaterialDark)]
    [InlineData("Skeuomorphism", false, AppThemeKind.Skeuomorphism)]
    [InlineData("Neumorphism", false, AppThemeKind.Neumorphism)]
    [InlineData("FrutigerAero", false, AppThemeKind.FrutigerAero)]
    [InlineData("Frutiger Aero", false, AppThemeKind.FrutigerAero)]
    [InlineData("Claymorphism", false, AppThemeKind.Claymorphism)]
    [InlineData("Clay", false, AppThemeKind.Claymorphism)]
    [InlineData("Glassmorphism", false, AppThemeKind.Glassmorphism)]
    [InlineData("Glass", false, AppThemeKind.Glassmorphism)]
    [InlineData("Y2KDigital", false, AppThemeKind.Y2KDigital)]
    [InlineData("Y2K", false, AppThemeKind.Y2KDigital)]
    [InlineData("Y2K Digital Aesthetic", false, AppThemeKind.Y2KDigital)]
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

            // 3. Explicit Y2KDigital theme -> preserves Y2KDigital and sets IsDarkTheme = true
            loadedLegacy.ThemeName = nameof(AppThemeKind.Y2KDigital);
            service.Save(loadedLegacy, tempFile);
            var loadedY2K = service.Load(tempFile);
            Assert.Equal("Y2KDigital", loadedY2K.ThemeName);
            Assert.True(loadedY2K.IsDarkTheme);
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    [Fact]
    public void ApplyTheme_SwitchesAllThemesAndUpdatesDynamicResources()
    {
        Exception? threadEx = null;
        var thread = new Thread(() =>
        {
            try
            {
                WpfTestHelper.EnsureApplication();

                var window = new MainWindow();

                Assert.Equal(8, ThemeService.AvailableThemes.Count);

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

                // Verify Claymorphism uses CornerRadius 18
                window.SetTheme(AppThemeKind.Claymorphism);
                var clayCorner = (CornerRadius)Application.Current!.TryFindResource("AppCardCornerRadius");
                Assert.Equal(18, clayCorner.TopLeft);

                // Verify Glassmorphism uses High Blur Shadow (28) and Translucent Glass Card
                window.SetTheme(AppThemeKind.Glassmorphism);
                var glassShadow = Application.Current!.TryFindResource("AppCardShadowEffect") as System.Windows.Media.Effects.DropShadowEffect;
                Assert.NotNull(glassShadow);
                Assert.Equal(28, glassShadow!.BlurRadius);

                // Verify Y2K Digital uses pure cyber terminal (#00FFA3) and cyan border
                window.SetTheme(AppThemeKind.Y2KDigital);
                var y2kTermFg = Application.Current!.TryFindResource("AppTerminalForegroundBrush") as SolidColorBrush;
                Assert.NotNull(y2kTermFg);
                Assert.Equal(Color.FromRgb(0x00, 0xFF, 0xA3), y2kTermFg!.Color);
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
