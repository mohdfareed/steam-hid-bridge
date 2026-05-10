using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using SteamHidBridge.App.Configuration;
using SteamHidBridge.App.Platform.App;

namespace SteamHidBridge.App.Ui.ViewModels;

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

        if (WriteSrmManifest())
        {
            SetActivity($"Saved {newId} and wrote Steam ROM Manager manifest.");
        }

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
        settingsStore.SaveGeneral(SelectedTheme, SelectedInputMode, SelectedOutputMode, TeensyPort, SrmManifestPath);
        applyInputMode(settingsStore.Document.General.InputMode);
        applyOutputMode(settingsStore.Document.General.OutputMode);
        applyTeensyPort(settingsStore.Document.General.TeensyPort);
        savedTheme = settingsStore.Document.General.Theme;
        savedInputMode = settingsStore.Document.General.InputMode;
        savedOutputMode = settingsStore.Document.General.OutputMode;
        savedTeensyPort = settingsStore.Document.General.TeensyPort;
        savedSrmManifestPath = settingsStore.Document.General.SrmManifestPath;
        saveGeneralCommand.RaiseCanExecuteChanged();
        if (WriteSrmManifest())
        {
            SetActivity($"Saved general settings and wrote Steam ROM Manager manifest for {settingsStore.Document.Games.Count} profile(s).");
        }

        return Task.CompletedTask;
    }

    private Task OpenAppDataAsync()
    {
        try
        {
            AppDataFolder.Open();
            SetActivity($"Opened {AppDataPaths.RootDirectory}.");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            SetError($"Could not open app data folder: {ex.Message}");
        }

        return Task.CompletedTask;
    }

    private Task InstallDriverAsync()
    {
        SetError("Virtual mouse driver install is disabled until signing/test-mode setup is resolved.");
        return Task.CompletedTask;
    }

    private bool WriteSrmManifest()
    {
        try
        {
            SrmManifestWriteResult result = srmManifestWriter.Write(SrmManifestPath, Environment.ProcessPath);
            AppLog.Write($"srm manifest written path={result.Path} profiles={result.ProfileCount}");
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or InvalidOperationException)
        {
            SetError($"Could not write Steam ROM Manager manifest: {ex.Message}");
            return false;
        }
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
        teensyPort = settingsStore.Document.General.TeensyPort;
        selectedInputMode = settingsStore.Document.General.InputMode;
        selectedOutputMode = settingsStore.Document.General.OutputMode;
        selectedTheme = settingsStore.Document.General.Theme;
        savedGameId = gameId;
        savedProfile = CloneProfile(profile);
        savedSrmManifestPath = srmManifestPath;
        savedTeensyPort = teensyPort;
        savedInputMode = selectedInputMode;
        savedOutputMode = selectedOutputMode;
        savedTheme = selectedTheme;
        runtime.SetProfile(gameId, profile);

        OnPropertyChanged(nameof(SelectedGameId));
        OnPropertyChanged(nameof(EditGameId));
        OnPropertyChanged(nameof(EditTitle));
        OnPropertyChanged(nameof(EditExecutable));
        OnPropertyChanged(nameof(EditArguments));
        OnPropertyChanged(nameof(EditWorkingDirectory));
        OnPropertyChanged(nameof(EditReceiverProcessesText));
        OnPropertyChanged(nameof(SrmManifestPath));
        OnPropertyChanged(nameof(TeensyPort));
        OnPropertyChanged(nameof(SelectedInputMode));
        OnPropertyChanged(nameof(SelectedOutputMode));
        OnPropertyChanged(nameof(SelectedTheme));
        OnPropertyChanged(nameof(ProfileText));
        OnPropertyChanged(nameof(ReceiverProcessesText));
        RaiseProfileCommandStateChanged();
        saveGeneralCommand.RaiseCanExecuteChanged();
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

    private static GameProfile CloneProfile(GameProfile profile)
    {
        return new GameProfile
        {
            Title = profile.Title,
            Executable = profile.Executable,
            Arguments = profile.Arguments,
            WorkingDirectory = profile.WorkingDirectory,
            ReceiverProcesses = [.. profile.ReceiverProcesses]
        };
    }

    private static bool ProfileEquals(GameProfile left, GameProfile right)
    {
        return string.Equals(left.Title, right.Title, StringComparison.Ordinal)
            && string.Equals(left.Executable, right.Executable, StringComparison.Ordinal)
            && string.Equals(left.Arguments, right.Arguments, StringComparison.Ordinal)
            && string.Equals(left.WorkingDirectory, right.WorkingDirectory, StringComparison.Ordinal)
            && left.ReceiverProcesses.SequenceEqual(right.ReceiverProcesses, StringComparer.Ordinal);
    }
}
