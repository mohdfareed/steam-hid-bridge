using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using SteamHidBridge.App.Infrastructure;

namespace SteamHidBridge.App.Profiles;

public sealed class AppSettings
{
    public string SrmManifestPath { get; set; } = "";

    public Dictionary<string, GameProfile> Games { get; set; } = [];
}

public sealed class AppSettingsStore(string path, AppSettings document)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    public string FilePath { get; } = path;
    public AppSettings Document { get; } = document;

    public static AppSettingsStore LoadDefault()
    {
        string path = AppDataPaths.SettingsPath;
        if (!File.Exists(path))
        {
            return new AppSettingsStore(path, Normalize(new AppSettings()));
        }

        string json = File.ReadAllText(path);
        AppSettings? document = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions);
        return new AppSettingsStore(path, Normalize(document));
    }

    public void SaveGame(string oldId, string newId, GameProfile profile)
    {
        Save(latest =>
        {
            if (!string.Equals(oldId, newId, StringComparison.OrdinalIgnoreCase))
            {
                _ = latest.Games.Remove(oldId);
            }

            latest.Games[newId] = profile;
        });
    }

    public void SaveSrmManifestPath(string path)
    {
        Save(latest => latest.SrmManifestPath = path.Trim());
    }

    private void Save(Action<AppSettings> update)
    {
        using Mutex mutex = new(false, BuildMutexName(FilePath));
        bool lockTaken = false;
        try
        {
            try
            {
                lockTaken = mutex.WaitOne();
            }
            catch (AbandonedMutexException)
            {
                lockTaken = true;
            }

            AppSettings latest = LoadFromPath(FilePath);
            update(latest);
            WriteAtomic(FilePath, latest);

            Document.SrmManifestPath = latest.SrmManifestPath;
            Document.Games.Clear();
            foreach ((string gameId, GameProfile gameProfile) in latest.Games)
            {
                Document.Games[gameId] = gameProfile;
            }
        }
        finally
        {
            if (lockTaken)
            {
                mutex.ReleaseMutex();
            }
        }
    }

    private static AppSettings LoadFromPath(string path)
    {
        if (!File.Exists(path))
        {
            return new AppSettings();
        }

        string json = File.ReadAllText(path);
        return Normalize(JsonSerializer.Deserialize<AppSettings>(json, JsonOptions));
    }

    private static AppSettings Normalize(AppSettings? document)
    {
        document ??= new AppSettings();
        if (string.IsNullOrWhiteSpace(document.SrmManifestPath))
        {
            document.SrmManifestPath = AppDataPaths.SrmManifestPath;
        }

        document.Games ??= [];
        return document;
    }

    private static void WriteAtomic(string path, AppSettings document)
    {
        string? directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            _ = Directory.CreateDirectory(directory);
        }

        string tempPath = path + ".tmp";
        string json = JsonSerializer.Serialize(document, JsonOptions);
        File.WriteAllText(tempPath, json);
        File.Move(tempPath, path, overwrite: true);
    }

    private static string BuildMutexName(string path)
    {
        string fullPath = Path.GetFullPath(path).ToUpperInvariant();
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(fullPath));
        return "Local\\SteamHidBridge.AppSettings." + Convert.ToHexString(hash);
    }
}
