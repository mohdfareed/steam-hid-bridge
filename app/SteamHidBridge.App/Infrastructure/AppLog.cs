using System;
using System.IO;
using System.Threading;

namespace SteamHidBridge.App.Infrastructure;

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
