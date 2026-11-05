namespace WorkManagement.Models;

public enum AppThemeKind
{
    MaterialLight,
    MaterialDark,
    Skeuomorphism,
    Neumorphism,
    FrutigerAero,
    Claymorphism,
    Glassmorphism,
    Y2KDigital
}

public sealed record AppThemeOption(
    AppThemeKind Kind,
    string Key,
    string DisplayName,
    string ShortDescription,
    string IconKind,
    bool IsDarkBase);
