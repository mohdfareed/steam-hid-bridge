using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Windows;
using SteamHidBridge.App.Configuration;

namespace SteamHidBridge.App.Platform.App;

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

public static class AppDataFolder
{
    public static void Open()
    {
        _ = Directory.CreateDirectory(AppDataPaths.RootDirectory);
        Process.Start(new ProcessStartInfo
        {
            FileName = AppDataPaths.RootDirectory,
            UseShellExecute = true
        });
    }
}

public static class AppDataPaths
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

public static class AppLog
{
    private static readonly Lock SyncLock = new();

    public static string FilePath => AppDataPaths.AppLogPath;

    public static void Write(string message)
    {
        lock (SyncLock)
        {
            Directory.CreateDirectory(AppDataPaths.LogDirectory);
            File.AppendAllText(AppDataPaths.AppLogPath, $"{DateTimeOffset.Now:O} info {message}{Environment.NewLine}");
        }
    }

    public static void WriteException(string message, Exception exception)
    {
        lock (SyncLock)
        {
            Directory.CreateDirectory(AppDataPaths.LogDirectory);
            File.AppendAllText(AppDataPaths.AppLogPath, $"{DateTimeOffset.Now:O} error {message}{Environment.NewLine}{exception}{Environment.NewLine}");
        }
    }
}
