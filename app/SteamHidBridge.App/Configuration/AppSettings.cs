using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using SteamHidBridge.App.Platform.App;

namespace SteamHidBridge.App.Configuration;

internal sealed class InvalidAppSettingsException(string path, string backupPath, Exception innerException)
    : Exception($"The settings file at '{path}' was invalid and has been backed up to '{backupPath}'.", innerException)
{
    public string BackupPath { get; } = backupPath;
}

internal sealed class AppSettingsStore(string path, AppSettings document)
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

    public static AppSettingsStore CreateDefault()
    {
        return new AppSettingsStore(AppDataPaths.SettingsPath, Normalize(new AppSettings()));
    }

    public static AppSettingsStore LoadDefault()
    {
        string path = AppDataPaths.SettingsPath;
        if (!File.Exists(path))
        {
            return new AppSettingsStore(path, new AppSettings());
        }

        try
        {
            string json = File.ReadAllText(path);
            AppSettings? document = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions);
            return new AppSettingsStore(path, Normalize(document));
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException)
        {
            string backupPath = BackupInvalidSettings(path);
            throw new InvalidAppSettingsException(path, backupPath, ex);
        }
    }

    public void Save()
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
            latest.General = Document.General;
            latest.Games.Clear();
            foreach ((string gameId, GameProfile gameProfile) in Document.Games)
            {
                latest.Games[gameId] = gameProfile;
            }

            WriteAtomic(FilePath, latest);
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
        if (string.IsNullOrWhiteSpace(document.General.SrmManifestPath))
        {
            document.General.SrmManifestPath = AppDataPaths.SrmManifestPath;
        }

        document.Games ??= [];
        foreach (GameProfile game in document.Games.Values)
        {
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
