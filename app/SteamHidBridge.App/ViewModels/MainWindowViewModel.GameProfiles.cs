using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using SteamHidBridge.App.Profiles;

namespace SteamHidBridge.App.ViewModels;

public sealed partial class MainWindowViewModel
{
    private Task NewGameAsync()
    {
        string gameId = CreateUniqueGameId();
        settingsStore.Document.Games[gameId] = new GameProfile();
        ReloadGameIds(gameId);
        AddLog($"Created {gameId}.");
        return Task.CompletedTask;
    }

    private Task SaveGameAsync()
    {
        if (string.IsNullOrWhiteSpace(EditGameId))
        {
            AddLog("Cannot save without an id.");
            return Task.CompletedTask;
        }

        string newId = EditGameId.Trim();
        if (!string.Equals(selectedGameId, newId, StringComparison.OrdinalIgnoreCase))
        {
            _ = settingsStore.Document.Games.Remove(selectedGameId);
        }

        settingsStore.SaveGame(selectedGameId, newId, ReadEditorProfile());
        ReloadGameIds(newId);
        AddLog($"Saved {newId}.");
        return Task.CompletedTask;
    }

    private Task LaunchGameAsync()
    {
        GameProfile profile = ReadEditorProfile();
        if (string.IsNullOrWhiteSpace(profile.Executable))
        {
            AddLog("No executable configured.");
            return Task.CompletedTask;
        }

        if (!File.Exists(profile.Executable))
        {
            AddLog($"Executable not found: {profile.Executable}");
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
                AddLog("Launch failed: process was not created.");
                return Task.CompletedTask;
            }

            TrackLaunchedProcess(process);
            AddLog($"Launched {selectedGameId}.");
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            AddLog($"Launch failed: {ex.Message}");
        }

        return Task.CompletedTask;
    }

    private Task CopySteamRomManagerJsonAsync()
    {
        string executable = Environment.ProcessPath ?? string.Empty;
        if (string.IsNullOrWhiteSpace(executable))
        {
            AddLog("Could not find bridge executable path.");
            return Task.CompletedTask;
        }

        string json = SteamRomManagerExport.CreateJson(settingsStore.Document.Games, executable);
        System.Windows.Clipboard.SetText(json);
        AddLog($"Copied Steam ROM Manager JSON for {settingsStore.Document.Games.Count} profile(s).");
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
                AddLog($"Stopping launched process {process.Id}.");
                process.Kill(entireProcessTree: true);
            }
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            AddLog($"Could not stop launched process: {ex.Message}");
        }
        finally
        {
            launchedProcess?.Dispose();
            launchedProcess = null;
            childProcessJob.Dispose();
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
            _ = statusTimer.Dispatcher.BeginInvoke(() => AddLog($"Launched process exited: {process.Id}"));
        };

        if (childProcessJob.TryAdd(process))
        {
            AddLog($"Tracking launched process tree: {process.Id}");
        }
        else
        {
            AddLog($"Tracking launched process directly only: {process.Id}");
        }
    }

    private void ReloadGameIds(string selectedId)
    {
        if (!settingsStore.Document.Games.TryGetValue(selectedId, out GameProfile? selectedProfile))
        {
            selectedProfile = new GameProfile();
            settingsStore.Document.Games[selectedId] = selectedProfile;
        }

        GameIds.Clear();
        foreach (string gameId in settingsStore.Document.Games.Keys)
        {
            GameIds.Add(gameId);
        }

        SelectedGameId = selectedId;
        LoadEditor(selectedId, selectedProfile);
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
