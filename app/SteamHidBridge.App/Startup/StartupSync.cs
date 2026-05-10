using System;
using System.IO;
using System.Text.Json;
using SteamHidBridge.App.Infrastructure;
using SteamHidBridge.App.Profiles;

namespace SteamHidBridge.App.Startup;

public static class StartupSync
{
    public static void Run(AppSettingsStore settingsStore)
    {
        try
        {
            settingsStore.SaveCurrent();
            AppLog.Write($"settings synced path={settingsStore.FilePath}");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or InvalidOperationException)
        {
            AppLog.WriteException("settings-sync-failed", ex);
        }

        try
        {
            SrmManifestWriteResult result = new SrmManifestWriter(settingsStore).Write(
                settingsStore.Document.General.SrmManifestPath,
                Environment.ProcessPath);
            AppLog.Write($"srm manifest synced path={result.Path} profiles={result.ProfileCount}");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or InvalidOperationException)
        {
            AppLog.WriteException("srm-manifest-sync-failed", ex);
        }
    }
}
