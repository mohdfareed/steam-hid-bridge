using System;
using System.IO;

namespace SteamHidBridge.App.Profiles;

public sealed class SrmManifestWriter(AppSettingsStore settingsStore)
{
    public SrmManifestWriteResult Write(string configuredPath, string? bridgeExecutable)
    {
        if (string.IsNullOrWhiteSpace(bridgeExecutable))
        {
            throw new InvalidOperationException("Could not find bridge executable path.");
        }

        string manifestPath = ExpandPath(configuredPath);
        if (string.IsNullOrWhiteSpace(manifestPath))
        {
            throw new InvalidOperationException("Steam ROM Manager manifest path is empty.");
        }

        string? directory = Path.GetDirectoryName(manifestPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            _ = Directory.CreateDirectory(directory);
        }

        string json = SteamRomManagerExport.CreateJson(settingsStore.Document.Games, bridgeExecutable);
        File.WriteAllText(manifestPath, json);
        return new SrmManifestWriteResult(manifestPath, settingsStore.Document.Games.Count);
    }

    private static string ExpandPath(string path)
    {
        path = Environment.ExpandEnvironmentVariables(path.Trim());
        if (path.StartsWith(@"~\", StringComparison.Ordinal) || path.StartsWith("~/", StringComparison.Ordinal))
        {
            string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            path = Path.Combine(home, path[2..]);
        }

        return path;
    }
}

public sealed record SrmManifestWriteResult(string Path, int ProfileCount);
