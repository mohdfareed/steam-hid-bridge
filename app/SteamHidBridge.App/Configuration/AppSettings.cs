using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using SteamHidBridge.App.Platform.App;

namespace SteamHidBridge.App.Configuration;

public sealed class AppSettingsStore(string path, AppSettings document)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    static AppSettingsStore()
    {
        JsonOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
    }

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

    public void SaveGeneral(AppTheme theme, BridgeInputMode inputMode, BridgeOutputMode outputMode, string teensyPort, string srmManifestPath)
    {
        Save(latest =>
        {
            latest.General.Theme = theme;
            latest.General.InputMode = inputMode;
            latest.General.OutputMode = outputMode;
            latest.General.TeensyPort = NormalizeTeensyPort(teensyPort);
            latest.General.SrmManifestPath = srmManifestPath.Trim();
        });
    }

    public void SaveCurrent()
    {
        Save(static _ =>
        {
        });
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

            Document.General.Theme = latest.General.Theme;
            Document.General.InputMode = latest.General.InputMode;
            Document.General.OutputMode = latest.General.OutputMode;
            Document.General.TeensyPort = latest.General.TeensyPort;
            Document.General.SrmManifestPath = latest.General.SrmManifestPath;
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
        document.General ??= new GeneralSettings();
        if (!Enum.IsDefined(document.General.InputMode))
        {
            document.General.InputMode = BridgeInputMode.LegacyMouse;
        }

        if (!Enum.IsDefined(document.General.OutputMode))
        {
            document.General.OutputMode = BridgeOutputMode.Teensy;
        }

        document.General.TeensyPort = NormalizeTeensyPort(document.General.TeensyPort);
        if (string.IsNullOrWhiteSpace(document.General.SrmManifestPath))
        {
            document.General.SrmManifestPath = AppDataPaths.SrmManifestPath;
        }

        document.Games ??= [];
        return document;
    }

    private static string NormalizeTeensyPort(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? "auto" : value.Trim();
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
