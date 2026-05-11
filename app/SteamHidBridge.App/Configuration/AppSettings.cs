using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using SteamHidBridge.App.Platform.App;

namespace SteamHidBridge.App.Configuration;

internal static class AppSettingsFile
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        Converters =
        {
            new System.Text.Json.Serialization.JsonStringEnumConverter(JsonNamingPolicy.CamelCase)
        }
    };

    public static AppSettings LoadDefault()
    {
        string path = AppDataPaths.SettingsPath;
        if (!File.Exists(path))
        {
            return new AppSettings();
        }

        try
        {
            AppSettings settings = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(path), JsonOptions) ?? new();
            return Normalize(settings);
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException)
        {
            string backupPath = BackupInvalidSettings(path);
            throw new InvalidDataException(
                $"The settings file was invalid and has been backed up.\n\nBackup:\n{backupPath}\n\nSteam HID Bridge started with empty settings.",
                ex);
        }
    }

    public static void SaveDefault(AppSettings settings)
    {
        string path = AppDataPaths.SettingsPath;
        using Mutex mutex = new(false, BuildMutexName(path));
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

            AppSettings latest = File.Exists(path)
                ? Normalize(JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(path), JsonOptions) ?? new AppSettings())
                : new AppSettings();

            AppSettings normalized = Normalize(settings);
            latest.General = normalized.General;
            latest.Games = normalized.Games;
            WriteAtomic(path, latest);
        }
        finally
        {
            if (lockTaken)
            {
                mutex.ReleaseMutex();
            }
        }
    }

    private static void WriteAtomic(string path, AppSettings settings)
    {
        string? directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            _ = Directory.CreateDirectory(directory);
        }

        string tempPath = path + ".tmp";
        File.WriteAllText(tempPath, JsonSerializer.Serialize(settings, JsonOptions));
        File.Move(tempPath, path, overwrite: true);
    }

    private static AppSettings Normalize(AppSettings settings)
    {
        settings.General ??= new GeneralSettings();
        settings.Games ??= [];

        settings.General.SrmManifestPath = string.IsNullOrWhiteSpace(settings.General.SrmManifestPath)
            ? AppDataPaths.SrmManifestPath
            : FileSystemPath.Normalize(settings.General.SrmManifestPath);

        if (string.IsNullOrWhiteSpace(settings.General.ViiperHost))
        {
            settings.General.ViiperHost = "localhost";
        }

        if (settings.General.ViiperPort <= 0)
        {
            settings.General.ViiperPort = 3242;
        }

        foreach (GameProfile profile in settings.Games.Values)
        {
            profile.Executable = FileSystemPath.Normalize(profile.Executable);
            profile.WorkingDirectory = FileSystemPath.Normalize(profile.WorkingDirectory);
            profile.ReceiverProcesses ??= [];
        }

        return settings;
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
        string stem = Path.GetFileNameWithoutExtension(path);
        string extension = Path.GetExtension(path);
        string timestamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
        string backupPath = Path.Combine(directory, $"{stem}.invalid-{timestamp}{extension}");
        int suffix = 2;

        while (File.Exists(backupPath))
        {
            backupPath = Path.Combine(directory, $"{stem}.invalid-{timestamp}-{suffix}{extension}");
            suffix++;
        }

        File.Move(path, backupPath);
        return backupPath;
    }
}
