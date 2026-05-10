using System.Windows;
using SteamHidBridge.App.Profiles;

namespace SteamHidBridge.App.Infrastructure;

public static class AppThemeManager
{
    public static void Apply(AppTheme theme)
    {
#pragma warning disable WPF0001 // ThemeMode is the official WPF Fluent theme API in .NET 9+.
        Application.Current.ThemeMode = theme switch
        {
            AppTheme.Light => ThemeMode.Light,
            AppTheme.Dark => ThemeMode.Dark,
            _ => ThemeMode.System
        };
#pragma warning restore WPF0001
    }
}
