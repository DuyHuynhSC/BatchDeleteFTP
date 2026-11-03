namespace BatchDeleteFTP.Models;

public enum AppThemeKind
{
    MaterialLight,
    MaterialDark,
    Skeuomorphism,
    Neumorphism,
    FrutigerAero
}

public sealed record AppThemeOption(
    AppThemeKind Kind,
    string Key,
    string DisplayName,
    string ShortDescription,
    string IconKind,
    bool IsDarkBase);
