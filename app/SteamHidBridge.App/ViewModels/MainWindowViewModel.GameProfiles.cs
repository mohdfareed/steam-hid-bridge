using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using SteamHidBridge.App.Infrastructure;
using SteamHidBridge.App.Profiles;

namespace SteamHidBridge.App.ViewModels;

public sealed partial class MainWindowViewModel
{
    private Task NewGameAsync()
    {
        string gameId = CreateUniqueGameId();
        settingsStore.Document.Games[gameId] = new GameProfile();
        ReloadGameIds(gameId);
        SetActivity($"Created {gameId}.");
        return Task.CompletedTask;
    }

    private Task SaveGameAsync()
    {
        if (string.IsNullOrWhiteSpace(EditGameId))
        {
            SetError("Cannot save without an id.");
            return Task.CompletedTask;
        }

        GameProfile profile = ReadEditorProfile();
        string newId = EditGameId.Trim();
        try
        {
            settingsStore.SaveGame(selectedGameId, newId, profile);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or InvalidOperationException)
        {
            SetError($"Save failed: {ex.Message}");
            return Task.CompletedTask;
        }

        if (settingsStore.Document.Games.TryGetValue(newId, out GameProfile? savedProfile))
        {
            ReloadGameIds(newId, savedProfile);
        }
        else
        {
            ReloadGameIds(newId);
        }

        SetActivity($"Saved {newId}.");
        return Task.CompletedTask;
    }

    private Task LaunchGameAsync()
    {
        runtime.SetProfile(selectedGameId, ReadEditorProfile());
        runtime.LaunchProfile();
        return Task.CompletedTask;
    }

    private Task SaveGeneralAsync()
    {
        settingsStore.SaveGeneral(SelectedTheme, SrmManifestPath);
        return WriteSrmManifestAsync(showSuccess: true);
    }

    private Task WriteSrmManifestOnStartup()
    {
        return WriteSrmManifestAsync(showSuccess: false);
    }

    private Task WriteSrmManifestAsync(bool showSuccess)
    {
        string executable = Environment.ProcessPath ?? string.Empty;
        if (string.IsNullOrWhiteSpace(executable))
        {
            SetError("Could not find bridge executable path.");
            return Task.CompletedTask;
        }

        string json = SteamRomManagerExport.CreateJson(settingsStore.Document.Games, executable);
        string manifestPath = ExpandPath(SrmManifestPath);
        if (string.IsNullOrWhiteSpace(manifestPath))
        {
            SetError("Steam ROM Manager manifest path is empty.");
            return Task.CompletedTask;
        }

        try
        {
            string? directory = Path.GetDirectoryName(manifestPath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                _ = Directory.CreateDirectory(directory);
            }
            File.WriteAllText(manifestPath, json);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or InvalidOperationException)
        {
            SetError($"Could not write Steam ROM Manager manifest: {ex.Message}");
            return Task.CompletedTask;
        }

        AppLog.Write($"srm manifest written path={manifestPath} profiles={settingsStore.Document.Games.Count}");
        if (showSuccess)
        {
            SetActivity($"Saved general settings and wrote manifest for {settingsStore.Document.Games.Count} profile(s).");
        }

        return Task.CompletedTask;
    }

    private void ReloadGameIds(string requestedId)
    {
        string selectedId = ResolveSelectedGameId(requestedId);
        if (!settingsStore.Document.Games.TryGetValue(selectedId, out GameProfile? selectedProfile))
        {
            selectedProfile = new GameProfile();
            settingsStore.Document.Games[selectedId] = selectedProfile;
        }

        ReloadGameIds(selectedId, selectedProfile);
    }

    private void ReloadGameIds(string selectedId, GameProfile selectedProfile)
    {
        isReloadingGameIds = true;
        try
        {
            SyncGameIds();
        }
        finally
        {
            isReloadingGameIds = false;
        }

        LoadEditor(selectedId, selectedProfile);
    }

    private void SyncGameIds()
    {
        List<string> sortedGameIds = [.. settingsStore.Document.Games.Keys.Order(StringComparer.OrdinalIgnoreCase)];

        for (int i = GameIds.Count - 1; i >= 0; i--)
        {
            if (!sortedGameIds.Contains(GameIds[i], StringComparer.OrdinalIgnoreCase))
            {
                GameIds.RemoveAt(i);
            }
        }

        for (int i = 0; i < sortedGameIds.Count; i++)
        {
            string gameId = sortedGameIds[i];
            int currentIndex = IndexOfGameId(gameId);
            if (currentIndex < 0)
            {
                GameIds.Insert(i, gameId);
            }
            else if (currentIndex != i)
            {
                GameIds.Move(currentIndex, i);
            }
        }
    }

    private int IndexOfGameId(string gameId)
    {
        for (int i = 0; i < GameIds.Count; i++)
        {
            if (string.Equals(GameIds[i], gameId, StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }

        return -1;
    }

    private string ResolveSelectedGameId(string requestedId)
    {
        requestedId = requestedId.Trim();
        if (!string.IsNullOrWhiteSpace(requestedId))
        {
            return requestedId;
        }

        foreach (string gameId in settingsStore.Document.Games.Keys.Order(StringComparer.OrdinalIgnoreCase))
        {
            return gameId;
        }

        return "new-game";
    }

    private void LoadEditor(string gameId, GameProfile profile)
    {
        selectedGameId = gameId;
        editGameId = gameId;
        editTitle = profile.Title;
        editExecutable = profile.Executable;
        editArguments = profile.Arguments;
        editWorkingDirectory = profile.WorkingDirectory;
        editReceiverProcessesText = string.Join(", ", profile.ReceiverProcesses);
        srmManifestPath = settingsStore.Document.General.SrmManifestPath;
        selectedTheme = settingsStore.Document.General.Theme;
        runtime.SetProfile(gameId, profile);

        OnPropertyChanged(nameof(SelectedGameId));
        OnPropertyChanged(nameof(EditGameId));
        OnPropertyChanged(nameof(EditTitle));
        OnPropertyChanged(nameof(EditExecutable));
        OnPropertyChanged(nameof(EditArguments));
        OnPropertyChanged(nameof(EditWorkingDirectory));
        OnPropertyChanged(nameof(EditReceiverProcessesText));
        OnPropertyChanged(nameof(SrmManifestPath));
        OnPropertyChanged(nameof(SelectedTheme));
        OnPropertyChanged(nameof(ProfileText));
        OnPropertyChanged(nameof(ReceiverProcessesText));
        OnPropertyChanged(nameof(ProfileInstanceText));
        OnPropertyChanged(nameof(ProcessText));
        OnPropertyChanged(nameof(WindowTitle));
        RaiseProfileCommandStateChanged();
    }

    private GameProfile ReadEditorProfile()
    {
        return new GameProfile
        {
            Title = EditTitle.Trim(),
            Executable = EditExecutable.Trim(),
            Arguments = EditArguments,
            WorkingDirectory = EditWorkingDirectory.Trim(),
            ReceiverProcesses = [.. ReceiverProcesses]
        };
    }

    private string CreateUniqueGameId()
    {
        const string prefix = "new-game";
        if (!settingsStore.Document.Games.ContainsKey(prefix))
        {
            return prefix;
        }

        int suffix = 2;
        while (settingsStore.Document.Games.ContainsKey($"{prefix}-{suffix}"))
        {
            suffix++;
        }

        return $"{prefix}-{suffix}";
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
