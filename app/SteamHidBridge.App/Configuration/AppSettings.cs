using System;
using System.Collections.Generic;
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

    public static AppSettingsLoadResult LoadDefault()
    {
        string path = AppDataPaths.SettingsPath;
        if (!File.Exists(path))
        {
            return new AppSettingsLoadResult(new AppSettingsStore(path, Normalize(new AppSettings())), null);
        }

        try
        {
            string json = File.ReadAllText(path);
            AppSettings? document = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions);
            return new AppSettingsLoadResult(new AppSettingsStore(path, Normalize(document)), null);
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException)
        {
            string backupPath = BackupInvalidSettings(path);
            return new AppSettingsLoadResult(
                new AppSettingsStore(path, Normalize(new AppSettings())),
                $"The settings file was invalid and has been backed up.\n\nBackup:\n{backupPath}\n\nSteam HID Bridge started with empty settings.");
        }
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

    public void SaveGeneral(AppTheme theme, string boardPort, string srmManifestPath)
    {
        Save(latest =>
        {
            latest.General.Theme = theme;
            latest.General.BoardPort = SerialPortSelection.Normalize(boardPort);
            latest.General.SrmManifestPath = srmManifestPath.Trim();
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
            Document.General.BoardPort = latest.General.BoardPort;
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
        document.General.BoardPort = SerialPortSelection.Normalize(document.General.BoardPort);
        if (string.IsNullOrWhiteSpace(document.General.SrmManifestPath))
        {
            document.General.SrmManifestPath = AppDataPaths.SrmManifestPath;
        }

        document.Games ??= [];
        foreach (GameProfile game in document.Games.Values)
        {
            if (!Enum.IsDefined(game.InputMode))
            {
                game.InputMode = BridgeInputMode.LegacyMouse;
            }

            if (!Enum.IsDefined(game.OutputMode))
            {
                game.OutputMode = BridgeOutputMode.None;
            }

            game.ReceiverProcesses ??= [];
        }
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

    private static string BackupInvalidSettings(string path)
    {
        string directory = Path.GetDirectoryName(path) ?? AppDataPaths.RootDirectory;
        string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(path);
        string extension = Path.GetExtension(path);
        string backupPath = Path.Combine(
            directory,
            $"{fileNameWithoutExtension}.invalid-{DateTime.Now:yyyyMMdd-HHmmss}{extension}");

        int suffix = 2;
        while (File.Exists(backupPath))
        {
            backupPath = Path.Combine(
                directory,
                $"{fileNameWithoutExtension}.invalid-{DateTime.Now:yyyyMMdd-HHmmss}-{suffix}{extension}");
            suffix++;
        }

        File.Move(path, backupPath);
        return backupPath;
    }

}

public sealed record AppSettingsLoadResult(AppSettingsStore Store, string? WarningMessage);
