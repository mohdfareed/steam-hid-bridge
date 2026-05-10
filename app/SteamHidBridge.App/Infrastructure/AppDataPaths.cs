using System;
using System.IO;

namespace SteamHidBridge.App.Infrastructure;

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
