using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using SteamHidBridge.App.Configuration;

namespace SteamHidBridge.App.Platform.App;

internal static class AppThemeManager
{
    public static void Apply(AppTheme theme)
    {
#pragma warning disable WPF0001 // ThemeMode is the official WPF Fluent theme API in .NET 9+.
        Application.Current.ThemeMode = theme switch
        {
            AppTheme.Light => ThemeMode.Light,
            AppTheme.Dark => ThemeMode.Dark,
            AppTheme.System => ThemeMode.System,
            _ => ThemeMode.System
        };
#pragma warning restore WPF0001
    }
}

internal static class AppDataFolder
{
    public static void Open()
    {
        _ = Directory.CreateDirectory(AppDataPaths.RootDirectory);
        _ = Process.Start(new ProcessStartInfo
        {
            FileName = AppDataPaths.RootDirectory,
            UseShellExecute = true
        });
    }
}

internal static class AppDataPaths
{
    public static string RootDirectory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "SteamHidBridge");

    public static string SettingsPath => Path.Combine(RootDirectory, "appsettings.json");

    public static string SrmManifestPath => Path.Combine(RootDirectory, "srm", "games.json");

    public static string LogDirectory => Path.Combine(RootDirectory, "logs");

    public static string AppLogPath => Path.Combine(LogDirectory, "app.log");

    public static string UpdateLogPath => Path.Combine(LogDirectory, "update.log");
}
