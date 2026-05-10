using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using SteamHidBridge.App.Infrastructure;
using SteamHidBridge.App.Input;
using SteamHidBridge.App.Profiles;
using SteamHidBridge.App.Runtime;
using SteamHidBridge.App.Startup;
using SteamHidBridge.App.Updates;
using SteamHidBridge.Protocol;

namespace SteamHidBridge.App.ViewModels;

public sealed partial class MainWindowViewModel : INotifyPropertyChanged
{
    private readonly BridgeLaunchOptions launchOptions;
    private readonly AppSettingsStore settingsStore;
    private readonly BridgeRuntime runtime;
    private readonly AppUpdater appUpdater;
    private readonly Func<AppUpdateCheckResult, bool> confirmUpdate;
    private readonly Action<Action> dispatch;
    private string selectedGameId = string.Empty;
    private string editGameId = string.Empty;
    private string editTitle = string.Empty;
    private string editExecutable = string.Empty;
    private string editArguments = string.Empty;
    private string editWorkingDirectory = string.Empty;
    private string editReceiverProcessesText = string.Empty;
    private string srmManifestPath = string.Empty;
    private bool isReloadingGameIds;
    private bool isForwardingActive;
    private HidInputReport lastReport;

    public MainWindowViewModel(
        BridgeLaunchOptions launchOptions,
        AppSettingsStore settingsStore,
        BridgeRuntime runtime,
        Func<AppUpdateCheckResult, bool> confirmUpdate,
        Action<Action> dispatch)
    {
        this.launchOptions = launchOptions;
        this.settingsStore = settingsStore;
        this.runtime = runtime;
        this.confirmUpdate = confirmUpdate;
        this.dispatch = dispatch;
        appUpdater = new AppUpdater();
        runtime.MouseInput += frame => dispatch(() => PreviewMouseInput(frame));
        runtime.StatusChanged += status => dispatch(() => ApplyRuntimeStatus(status));
        runtime.ExitRequested += RequestExit;

        NewGameCommand = new AsyncRelayCommand(NewGameAsync);
        SaveGameCommand = new AsyncRelayCommand(SaveGameAsync);
        LaunchGameCommand = new AsyncRelayCommand(LaunchGameAsync);
        WriteSteamRomManagerManifestCommand = new AsyncRelayCommand(WriteSteamRomManagerManifestAsync);
        CheckForUpdateCommand = new AsyncRelayCommand(CheckForUpdateAsync);

        ReloadGameIds(launchOptions.ProfileId);
        WriteSrmManifestOnStartup();
        SetActivity($"Ready. profile={selectedGameId}");
        AppLog.Write($"settings={settingsStore.FilePath}");

        if (launchOptions.LaunchGame && !string.IsNullOrWhiteSpace(launchOptions.ProfileId))
        {
            _ = LaunchGameAsync();
        }
        else if (launchOptions.LaunchGame)
        {
            SetActivity("Launch mode requires --profile <id>.");
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    public event Action<int>? ExitRequested;

    public ObservableCollection<string> GameIds { get; } = [];
    public ICommand NewGameCommand { get; }
    public ICommand SaveGameCommand { get; }
    public ICommand LaunchGameCommand { get; }
    public ICommand WriteSteamRomManagerManifestCommand { get; }
    public ICommand CheckForUpdateCommand { get; }

    public string SelectedGameId
    {
        get => selectedGameId;
        set
        {
            if (isReloadingGameIds)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(value) || selectedGameId == value)
            {
                return;
            }

            selectedGameId = value;
            OnPropertyChanged();
            LoadEditor(value, settingsStore.Document.Games[value]);
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
            }
        }
    }

    public string EditExecutable
    {
        get => editExecutable;
        set => SetProperty(ref editExecutable, value);
    }

    public string EditTitle
    {
        get => editTitle;
        set
        {
            if (SetProperty(ref editTitle, value))
            {
                OnPropertyChanged(nameof(ProfileText));
            }
        }
    }

    public string EditArguments
    {
        get => editArguments;
        set => SetProperty(ref editArguments, value);
    }

    public string EditWorkingDirectory
    {
        get => editWorkingDirectory;
        set => SetProperty(ref editWorkingDirectory, value);
    }

    public string EditReceiverProcessesText
    {
        get => editReceiverProcessesText;
        set
        {
            if (SetProperty(ref editReceiverProcessesText, value))
            {
                OnPropertyChanged(nameof(ReceiverProcessesText));
                runtime.SetProfile(selectedGameId, ReadEditorProfile());
            }
        }
    }

    public string ProfileText => string.IsNullOrWhiteSpace(EditTitle) ? EditGameId : EditTitle;
    public string ReceiverProcessesText => ReceiverProcesses.Length == 0 ? "None configured" : string.Join(", ", ReceiverProcesses);

    public string SrmManifestPath
    {
        get => srmManifestPath;
        set => SetProperty(ref srmManifestPath, value);
    }

    public string ForwardingStatus
    {
        get;
        private set => SetProperty(ref field, value);
    } = "Forwarding off";

    public string LastOutputText
    {
        get;
        private set => SetProperty(ref field, value);
    } = "No output yet";

    public string InputLoopText
    {
        get;
        private set => SetProperty(ref field, value);
    } = "input loop starting";

    public string ActivityText
    {
        get;
        private set => SetProperty(ref field, value);
    } = "Ready";

    public string PointerText => $"dx {lastReport.PointerDeltaX}, dy {lastReport.PointerDeltaY}";
    public string WheelText => $"wheel {lastReport.VerticalWheel}";
    public string MouseButtonsText => lastReport.MouseButtons == MouseButtons.None ? "buttons none" : $"buttons {lastReport.MouseButtons}";
    public string MouseLeftBrush => MouseButtonBrush(MouseButtons.Left);
    public string MouseRightBrush => MouseButtonBrush(MouseButtons.Right);
    public string MouseMiddleBrush => MouseButtonBrush(MouseButtons.Middle);
    public string MouseBackBrush => MouseButtonBrush(MouseButtons.Back);
    public string MouseForwardBrush => MouseButtonBrush(MouseButtons.Forward);
    public string StatusBrush => isForwardingActive ? "SeaGreen" : "Gray";
    public string VersionText => $"Version {appUpdater.CurrentVersionText}";
    public string InstanceText => $"{selectedGameId} - PID {Environment.ProcessId}";
    public string WindowTitle => $"Steam HID Bridge - {InstanceText}";

    private string[] ReceiverProcesses => ParseReceiverProcesses(EditReceiverProcessesText);

    private void SetActivity(string message)
    {
        ActivityText = message;
        AppLog.Write(message);
    }

    private bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private void RequestExit(int exitCode)
    {
        ExitRequested?.Invoke(exitCode);
    }

    private string MouseButtonBrush(MouseButtons button)
    {
        return lastReport.MouseButtons.HasFlag(button) ? "SeaGreen" : "White";
    }
}
