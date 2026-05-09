using System;
using System.IO;
using System.Threading;

namespace SteamHidBridge.App.Infrastructure;

public static class AppLog
{
    private static readonly Lock SyncLock = new();

    public static void Write(string message)
    {
        string logPath = Path.Combine(AppContext.BaseDirectory, "SteamHidBridge.lifecycle.log");
        lock (SyncLock)
        {
            File.AppendAllText(logPath, $"{DateTimeOffset.Now:O} {message}{Environment.NewLine}");
        }
    }

    public static void WriteException(string message, Exception exception)
    {
        string logPath = Path.Combine(AppContext.BaseDirectory, "SteamHidBridge.error.log");
        lock (SyncLock)
        {
            File.AppendAllText(logPath, $"{DateTimeOffset.Now:O} {message}{Environment.NewLine}{exception}{Environment.NewLine}");
        }
    }
}
