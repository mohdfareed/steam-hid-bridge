using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Input;
using SteamHidBridge.App.Configuration;
using SteamHidBridge.App.Core;
using SteamHidBridge.App.Platform.App;

namespace SteamHidBridge.App.Ui.ViewModels;

internal sealed class ProfileSettingsViewModel : ObservableObject
{
    public sealed record SettingOption<T>(T Value, string Label);

    private readonly BridgeAppService appService;
    private readonly BridgeSession session;
    private readonly string activeProfileId;
    private string selectedGameId = string.Empty;
    private string editGameId = string.Empty;
    private string editTitle = string.Empty;
    private string editExecutable = string.Empty;
    private string editArguments = string.Empty;
    private string editWorkingDirectory = string.Empty;
    private string editReceiverProcessesText = string.Empty;
    private BridgeOutputMode selectedOutputMode;
    private string savedGameId = string.Empty;
    private GameProfile savedProfile = new();
    private bool isReloadingGameIds;
    private readonly AsyncRelayCommand saveGameCommand;
    private readonly AsyncRelayCommand launchGameCommand;

    public ProfileSettingsViewModel(BridgeAppService appService, BridgeSession session, string activeProfileId)
    {
        this.appService = appService;
        this.session = session;
        this.activeProfileId = activeProfileId.Trim();

        NewGameCommand = new AsyncRelayCommand(NewGameAsync);
        saveGameCommand = new AsyncRelayCommand(SaveGameAsync, CanSaveProfile);
        launchGameCommand = new AsyncRelayCommand(LaunchGameAsync, CanSaveOrLaunchProfile);
        SaveGameCommand = saveGameCommand;
        LaunchGameCommand = launchGameCommand;

        ReloadGameIds(activeProfileId);
    }

    public ObservableCollection<string> GameIds { get; } = [];

    public ObservableCollection<SettingOption<BridgeOutputMode>> OutputModeOptions { get; } =
    [
        new(BridgeOutputMode.None, "None"),
        new(BridgeOutputMode.Board, "Physical Mouse")
    ];

    public ICommand NewGameCommand { get; }
    public ICommand SaveGameCommand { get; }
    public ICommand LaunchGameCommand { get; }

    public string SelectedGameId
    {
        get => selectedGameId;
        set
        {
            if (isReloadingGameIds || string.IsNullOrWhiteSpace(value) || selectedGameId == value)
            {
                return;
            }

            selectedGameId = value;
            OnPropertyChanged();
            LoadEditor(value, appService.GetOrCreateProfile(value));
        }
    }

    public string EditGameId
    {
        get => editGameId;
        set
        {
            if (SetProperty(ref editGameId, value))
            {
                OnPropertyChanged(nameof(ProfileText));
                RaiseCommandStateChanged();
            }
        }
    }

    public string EditTitle
    {
        get => editTitle;
        set
        {
            if (SetProperty(ref editTitle, value))
            {
                OnPropertyChanged(nameof(ProfileText));
                RaiseCommandStateChanged();
            }
        }
    }

    public string EditExecutable
    {
        get => editExecutable;
        set
        {
            if (SetProperty(ref editExecutable, value))
            {
                RaiseCommandStateChanged();
            }
        }
    }

    public string EditArguments
    {
        get => editArguments;
        set
        {
            if (SetProperty(ref editArguments, value))
            {
                RaiseCommandStateChanged();
            }
        }
    }

    public string EditWorkingDirectory
    {
        get => editWorkingDirectory;
        set
        {
            if (SetProperty(ref editWorkingDirectory, value))
            {
                RaiseCommandStateChanged();
            }
        }
    }

    public string EditReceiverProcessesText
    {
        get => editReceiverProcessesText;
        set
        {
            if (SetProperty(ref editReceiverProcessesText, value))
            {
                OnPropertyChanged(nameof(ReceiverProcessesText));
                SyncSessionProfileIfActive();
                RaiseCommandStateChanged();
            }
        }
    }

    public BridgeOutputMode SelectedOutputMode
    {
        get => selectedOutputMode;
        set
        {
            if (SetProperty(ref selectedOutputMode, value))
            {
                SyncSessionProfileIfActive();
                RaiseCommandStateChanged();
            }
        }
    }

    public string ProfileText => string.IsNullOrWhiteSpace(EditTitle) ? EditGameId : EditTitle;
    public string ReceiverProcessesText => ReceiverProcesses.Length == 0 ? "None configured" : string.Join(", ", ReceiverProcesses);

    public Task LaunchRequestedProfileAsync()
    {
        return string.IsNullOrWhiteSpace(activeProfileId) ? Task.CompletedTask : LaunchGameAsync();
    }

    private string[] ReceiverProcesses => ParseReceiverProcesses(EditReceiverProcessesText);

    private Task NewGameAsync()
    {
        string gameId = appService.CreateUniqueGameId();
        _ = appService.GetOrCreateProfile(gameId);
        ReloadGameIds(gameId);
        return Task.CompletedTask;
    }

    private Task SaveGameAsync()
    {
        if (string.IsNullOrWhiteSpace(EditGameId))
        {
            UserDialogs.ShowError("Cannot save without an id.");
            return Task.CompletedTask;
        }

        try
        {
            string newId = EditGameId.Trim();
            appService.SaveProfile(selectedGameId, newId, ReadEditorProfile());
            ReloadGameIds(newId, appService.GetOrCreateProfile(newId));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or InvalidOperationException)
        {
            UserDialogs.ShowError($"Save failed: {ex.Message}");
        }

        return Task.CompletedTask;
    }

    private Task LaunchGameAsync()
    {
        session.SetOutputMode(SelectedOutputMode);
        session.SetProfile(ReadEditorProfile());

        try
        {
            session.LaunchProfile();
        }
        catch (Exception ex) when (ex is FileNotFoundException or InvalidOperationException or Win32Exception)
        {
            UserDialogs.ShowError(ex.Message);
        }

        return Task.CompletedTask;
    }

    private bool CanSaveOrLaunchProfile()
    {
        return !string.IsNullOrWhiteSpace(EditGameId)
            && !string.IsNullOrWhiteSpace(EditExecutable)
            && ReceiverProcesses.Length > 0;
    }

    private bool CanSaveProfile()
    {
        return CanSaveOrLaunchProfile() && HasProfileChanges();
    }

    private bool HasProfileChanges()
    {
        return !string.Equals(savedGameId, EditGameId.Trim(), StringComparison.Ordinal)
            || !savedProfile.ContentEquals(ReadEditorProfile());
    }

    private void RaiseCommandStateChanged()
    {
        saveGameCommand.RaiseCanExecuteChanged();
        launchGameCommand.RaiseCanExecuteChanged();
    }

    private void ReloadGameIds(string requestedId)
    {
        string selectedId = appService.ResolveSelectedGameId(requestedId);
        ReloadGameIds(selectedId, appService.GetOrCreateProfile(selectedId));
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
        List<string> sortedGameIds = [.. appService.GetGameIds()];
        for (int index = GameIds.Count - 1; index >= 0; index--)
        {
            if (!sortedGameIds.Contains(GameIds[index], StringComparer.OrdinalIgnoreCase))
            {
                GameIds.RemoveAt(index);
            }
        }

        for (int index = 0; index < sortedGameIds.Count; index++)
        {
            string gameId = sortedGameIds[index];
            int currentIndex = IndexOfGameId(gameId);
            if (currentIndex < 0)
            {
                GameIds.Insert(index, gameId);
            }
            else if (currentIndex != index)
            {
                GameIds.Move(currentIndex, index);
            }
        }
    }

    private int IndexOfGameId(string gameId)
    {
        for (int index = 0; index < GameIds.Count; index++)
        {
            if (string.Equals(GameIds[index], gameId, StringComparison.OrdinalIgnoreCase))
            {
                return index;
            }
        }

        return -1;
    }

    private void LoadEditor(string gameId, GameProfile profile)
    {
        selectedGameId = gameId;
        editGameId = gameId;
        editTitle = profile.Title;
        editExecutable = profile.Executable;
        editArguments = profile.Arguments;
        editWorkingDirectory = profile.WorkingDirectory;
        selectedOutputMode = profile.OutputMode;
        editReceiverProcessesText = string.Join(" | ", profile.ReceiverProcesses);
        savedGameId = gameId;
        savedProfile = profile.Copy();
        SyncSessionProfileIfActive();

        OnPropertyChanged(nameof(SelectedGameId));
        OnPropertyChanged(nameof(EditGameId));
        OnPropertyChanged(nameof(EditTitle));
        OnPropertyChanged(nameof(EditExecutable));
        OnPropertyChanged(nameof(EditArguments));
        OnPropertyChanged(nameof(EditWorkingDirectory));
        OnPropertyChanged(nameof(EditReceiverProcessesText));
        OnPropertyChanged(nameof(SelectedOutputMode));
        OnPropertyChanged(nameof(ProfileText));
        OnPropertyChanged(nameof(ReceiverProcessesText));
        RaiseCommandStateChanged();
    }

    private void SyncSessionProfileIfActive()
    {
        if (!string.IsNullOrWhiteSpace(activeProfileId)
            && string.Equals(selectedGameId, activeProfileId, StringComparison.OrdinalIgnoreCase))
        {
            session.SetOutputMode(SelectedOutputMode);
            session.SetProfile(ReadEditorProfile());
        }
    }

    private GameProfile ReadEditorProfile()
    {
        return new GameProfile
        {
            Title = EditTitle.Trim(),
            Executable = EditExecutable.Trim(),
            Arguments = EditArguments,
            WorkingDirectory = EditWorkingDirectory.Trim(),
            OutputMode = SelectedOutputMode,
            ReceiverProcesses = [.. ReceiverProcesses]
        };
    }

    private static string[] ParseReceiverProcesses(string value)
    {
        return value.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }
}
