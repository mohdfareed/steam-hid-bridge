using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading.Tasks;
using SteamHidBridge.App.Infrastructure;
using SteamHidBridge.App.Profiles;
using SteamHidBridge.App.Windows;

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
            SetActivity("Cannot save without an id.");
            return Task.CompletedTask;
        }

        string newId = EditGameId.Trim();
        try
        {
            settingsStore.SaveGame(selectedGameId, newId, ReadEditorProfile());
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or InvalidOperationException)
        {
            SetActivity($"Save failed: {ex.Message}");
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
        GameProfile profile = ReadEditorProfile();
        if (string.IsNullOrWhiteSpace(profile.Executable))
        {
            SetActivity("No executable configured.");
            return Task.CompletedTask;
        }

        if (!File.Exists(profile.Executable))
        {
            SetActivity($"Executable not found: {profile.Executable}");
            return Task.CompletedTask;
        }

        if (launchedProcess is { HasExited: false })
        {
            SetActivity("A launched process is already running.");
            return Task.CompletedTask;
        }

        string workingDirectory = string.IsNullOrWhiteSpace(profile.WorkingDirectory)
            ? Path.GetDirectoryName(profile.Executable) ?? AppContext.BaseDirectory
            : profile.WorkingDirectory;

        try
        {
            Process? process = Process.Start(new ProcessStartInfo
            {
                FileName = profile.Executable,
                Arguments = profile.Arguments,
                WorkingDirectory = workingDirectory,
                UseShellExecute = false
            });

            if (process is null)
            {
                SetActivity("Launch failed: process was not created.");
                return Task.CompletedTask;
            }

            TrackLaunchedProcess(process);
            SetActivity($"Launched {selectedGameId}.");
        }
        catch (Exception ex) when (ex is InvalidOperationException or Win32Exception)
        {
            SetActivity($"Launch failed: {ex.Message}");
        }

        return Task.CompletedTask;
    }

    private Task CopySteamRomManagerJsonAsync()
    {
        string executable = Environment.ProcessPath ?? string.Empty;
        if (string.IsNullOrWhiteSpace(executable))
        {
            SetActivity("Could not find bridge executable path.");
            return Task.CompletedTask;
        }

        string json = SteamRomManagerExport.CreateJson(settingsStore.Document.Games, executable);
        try
        {
            System.Windows.Clipboard.SetText(json);
        }
        catch (Exception ex) when (ex is ExternalException or InvalidOperationException)
        {
            SetActivity($"Could not copy Steam ROM Manager JSON: {ex.Message}");
            return Task.CompletedTask;
        }

        SetActivity($"Copied Steam ROM Manager JSON for {settingsStore.Document.Games.Count} profile(s).");
        return Task.CompletedTask;
    }

    public void StopLaunchedProcesses()
    {
        isStoppingLaunchedProcesses = true;
        statusTimer.Stop();

        try
        {
            if (launchedProcess is { HasExited: false } process)
            {
                SetActivity($"Stopping launched process {process.Id}.");
                process.Kill(entireProcessTree: true);
            }
        }
        catch (Exception ex) when (ex is InvalidOperationException or Win32Exception)
        {
            SetActivity($"Could not stop launched process: {ex.Message}");
        }
        finally
        {
            launchedProcess?.Dispose();
            launchedProcess = null;
            childProcessJob?.Dispose();
            childProcessJob = null;
        }
    }

    private void TrackLaunchedProcess(Process process)
    {
        launchedProcess?.Dispose();
        launchedProcess = process;
        launchedProcessExited = false;
        hasSeenReceiverProcess = false;

        process.EnableRaisingEvents = true;
        process.Exited += (_, _) =>
        {
            launchedProcessExited = true;
            _ = statusTimer.Dispatcher.BeginInvoke(() => SetActivity($"Launched process exited: {process.Id}"));
        };

        if (TryTrackProcessTree(process))
        {
            AppLog.Write($"tracking launched process tree={process.Id}");
        }
        else
        {
            AppLog.Write($"tracking launched process directly={process.Id}");
        }
    }

    private bool TryTrackProcessTree(Process process)
    {
        try
        {
            childProcessJob ??= new ChildProcessJob();
            return childProcessJob.TryAdd(process);
        }
        catch (Exception ex) when (ex is InvalidOperationException or Win32Exception)
        {
            AppLog.WriteException("child-process-job-unavailable", ex);
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
        hasSeenReceiverProcess = false;

        OnPropertyChanged(nameof(SelectedGameId));
        OnPropertyChanged(nameof(EditGameId));
        OnPropertyChanged(nameof(EditTitle));
        OnPropertyChanged(nameof(EditExecutable));
        OnPropertyChanged(nameof(EditArguments));
        OnPropertyChanged(nameof(EditWorkingDirectory));
        OnPropertyChanged(nameof(EditReceiverProcessesText));
        OnPropertyChanged(nameof(ProfileText));
        OnPropertyChanged(nameof(ReceiverProcessesText));
        OnPropertyChanged(nameof(InstanceText));
        OnPropertyChanged(nameof(WindowTitle));
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
}
