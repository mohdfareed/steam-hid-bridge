using System;
using System.IO;
using System.Threading;

namespace SteamHidBridge.App.Infrastructure;

public static class AppLog
{
    private static readonly Lock SyncLock = new();
    private static readonly string LogDirectory = Path.Combine(AppContext.BaseDirectory, "logs");
    private static readonly string AppLogPath = Path.Combine(LogDirectory, "app.log");

    public static string FilePath => AppLogPath;

    public static void Write(string message)
    {
        lock (SyncLock)
        {
            Directory.CreateDirectory(LogDirectory);
            File.AppendAllText(AppLogPath, $"{DateTimeOffset.Now:O} info {message}{Environment.NewLine}");
        }
    }

    public static void WriteException(string message, Exception exception)
    {
        lock (SyncLock)
        {
            Directory.CreateDirectory(LogDirectory);
            File.AppendAllText(AppLogPath, $"{DateTimeOffset.Now:O} error {message}{Environment.NewLine}{exception}{Environment.NewLine}");
        }
    }
}
