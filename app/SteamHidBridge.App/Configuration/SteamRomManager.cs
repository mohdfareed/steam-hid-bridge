using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.Json;

namespace SteamHidBridge.App.Configuration;

internal sealed record SrmManifestWriteResult(string Path, int ProfileCount);

internal sealed class SrmManifestWriter(AppSettingsStore settingsStore)
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


internal static class SteamRomManagerExport
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    public static string CreateJson(IReadOnlyDictionary<string, GameProfile> games, string bridgeExecutable)
    {
        string startIn = Path.GetDirectoryName(bridgeExecutable) ?? string.Empty;
        List<SteamRomManagerEntry> entries = [];

        foreach ((string id, GameProfile profile) in games)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                continue;
            }

            entries.Add(new SteamRomManagerEntry(
                Title: GetTitle(id, profile),
                Target: bridgeExecutable,
                StartIn: startIn,
                LaunchOptions: $"--profile {QuoteArgument(id)}",
                AppendArgsToExecutable: false));
        }

        return JsonSerializer.Serialize(entries, JsonOptions);
    }

    private static string GetTitle(string id, GameProfile profile)
    {
        if (!string.IsNullOrWhiteSpace(profile.Title))
        {
            return profile.Title.Trim();
        }

        string spaced = id.Replace('-', ' ').Replace('_', ' ');
        return CultureInfo.CurrentCulture.TextInfo.ToTitleCase(spaced);
    }

    private static string QuoteArgument(string value)
    {
        return !value.Contains(' ') && !value.Contains('"') ? value : $"\"{value.Replace("\"", "\\\"")}\"";
    }

    private sealed record SteamRomManagerEntry(
        string Title,
        string Target,
        string StartIn,
        string LaunchOptions,
        bool AppendArgsToExecutable);
}
