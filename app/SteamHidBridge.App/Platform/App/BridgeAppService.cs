using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SteamHidBridge.App.Configuration;

namespace SteamHidBridge.App.Platform.App;

internal sealed class BridgeAppService(AppSettings settings)
{
    private readonly AppUpdater appUpdater = new();
    private readonly TeensyFirmwareUpdater teensyFirmwareUpdater = new();

    public static string AppDataPath => AppDataPaths.RootDirectory;
    public string VersionText => appUpdater.CurrentVersionText;
    public bool HasBundledFirmware => teensyFirmwareUpdater.HasBundledFirmware;
    public AppTheme Theme => settings.General.Theme;
    public int? BoardPort => settings.General.BoardPort;
    public string SrmManifestPath => settings.General.SrmManifestPath;

    public IReadOnlyList<string> GetGameIds()
    {
        return [.. settings.Games.Keys.Order(StringComparer.OrdinalIgnoreCase)];
    }

    public GameProfile GetOrCreateProfile(string id)
    {
        if (!settings.Games.TryGetValue(id, out GameProfile? profile))
        {
            profile = new GameProfile();
            settings.Games[id] = profile;
        }

        return profile;
    }

    public string ResolveSelectedGameId(string requestedId)
    {
        requestedId = requestedId.Trim();
        if (!string.IsNullOrWhiteSpace(requestedId))
        {
            return requestedId;
        }

        foreach (string gameId in settings.Games.Keys.Order(StringComparer.OrdinalIgnoreCase))
        {
            return gameId;
        }

        return CreateUniqueGameId();
    }

    public string CreateUniqueGameId()
    {
        const string prefix = "new-game";
        if (!settings.Games.ContainsKey(prefix))
        {
            return prefix;
        }

        int suffix = 2;
        while (settings.Games.ContainsKey($"{prefix}-{suffix}"))
        {
            suffix++;
        }

        return $"{prefix}-{suffix}";
    }

    public void SaveProfile(string originalId, string newId, GameProfile profile)
    {
        if (!string.Equals(originalId, newId, StringComparison.OrdinalIgnoreCase))
        {
            _ = settings.Games.Remove(originalId);
        }

        settings.Games[newId] = profile;
        AppSettingsFile.SaveDefault(settings);
    }

    public void SaveGeneral(AppTheme theme, int? boardPort, string srmManifestPath)
    {
        settings.General.Theme = theme;
        settings.General.BoardPort = boardPort;
        settings.General.SrmManifestPath = srmManifestPath.Trim();
        AppSettingsFile.SaveDefault(settings);
    }

    public void ExportSrmManifest()
    {
        SrmManifestWriter.Write(settings, Environment.ProcessPath);
    }

    public static void OpenAppData()
    {
        AppDataFolder.Open();
    }

    public Task UpdateFirmwareAsync(CancellationToken cancellationToken = default)
    {
        return teensyFirmwareUpdater.UpdateAsync(cancellationToken);
    }

    public Task<AppUpdateCheckResult> CheckForUpdateAsync(CancellationToken cancellationToken = default)
    {
        return appUpdater.CheckLatestAsync(cancellationToken);
    }

    public static Task StartUpdateAsync(AppUpdateCheckResult update, CancellationToken cancellationToken = default)
    {
        return AppUpdater.StartUpdateAsync(update, cancellationToken);
    }
}
