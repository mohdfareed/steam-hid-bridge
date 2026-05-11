using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using SteamHidBridge.App.Configuration;
using SteamHidBridge.App.Core;
using SteamHidBridge.App.Platform;
using SteamHidBridge.App.Platform.App;
using SteamHidBridge.App.Platform.Board;
using SteamHidBridge.Protocol;

namespace SteamHidBridge.App.Ui.ViewModels;

internal sealed partial class MainWindowViewModel : INotifyPropertyChanged
{
    public sealed record SettingOption<T>(T Value, string Label);

    private readonly AppSettings settings;
    private readonly BridgeRuntime runtime;
    private readonly AppUpdater appUpdater;
    private readonly TeensyFirmwareUpdater teensyFirmwareUpdater;
    private readonly Action<BridgeInputMode> applyInputMode;
    private readonly Action<BridgeOutputMode> applyOutputMode;
    private readonly Action<int?> applyBoardPort;
    private readonly Action<AppTheme> applyTheme;
    private readonly Func<AppUpdateCheckResult, bool> confirmUpdate;
    private readonly string activeProfileId;
    private string selectedGameId = string.Empty;
    private string editGameId = string.Empty;
    private string editTitle = string.Empty;
    private string editExecutable = string.Empty;
    private string editArguments = string.Empty;
    private string editWorkingDirectory = string.Empty;
    private string editReceiverProcessesText = string.Empty;
    private string srmManifestPath = string.Empty;
    private string boardPort = string.Empty;
    private BridgeInputMode selectedInputMode;
    private BridgeOutputMode selectedOutputMode;
    private string savedGameId = string.Empty;
    private GameProfile savedProfile = new();
    private string savedSrmManifestPath = string.Empty;
    private string savedBoardPort = string.Empty;
    private AppTheme savedTheme;
    private AppTheme selectedTheme;
    private bool isReloadingGameIds;
    private bool isForwardingActive;
    private HidInputReport lastReport;
    private readonly AsyncRelayCommand saveGameCommand;
    private readonly AsyncRelayCommand launchGameCommand;
    private readonly AsyncRelayCommand saveGeneralCommand;
    private readonly AsyncRelayCommand updateFirmwareCommand;

    public MainWindowViewModel(
        BridgeLaunchOptions launchOptions,
        AppSettings settings,
        BridgeRuntime runtime,
        Action<BridgeInputMode> applyInputMode,
        Action<BridgeOutputMode> applyOutputMode,
        Action<int?> applyBoardPort,
        Action<AppTheme> applyTheme,
        Func<AppUpdateCheckResult, bool> confirmUpdate,
        Action<Action> dispatch)
    {
        this.settings = settings;
        this.runtime = runtime;
        teensyFirmwareUpdater = new TeensyFirmwareUpdater();
        this.applyInputMode = applyInputMode;
        this.applyOutputMode = applyOutputMode;
        this.applyBoardPort = applyBoardPort;
        this.applyTheme = applyTheme;
        this.confirmUpdate = confirmUpdate;
        activeProfileId = launchOptions.ProfileId.Trim();
        appUpdater = new AppUpdater();
        runtime.MouseInput += frame => dispatch(() => PreviewMouseInput(frame));
        runtime.StatusChanged += status => dispatch(() => ApplyRuntimeStatus(status));
        runtime.ExitRequested += exitCode => dispatch(() => RequestExit(exitCode));

        NewGameCommand = new AsyncRelayCommand(NewGameAsync);
        saveGameCommand = new AsyncRelayCommand(SaveGameAsync, CanSaveProfile);
        launchGameCommand = new AsyncRelayCommand(LaunchGameAsync, CanSaveOrLaunchProfile);
        saveGeneralCommand = new AsyncRelayCommand(SaveGeneralAsync, HasGeneralChanges);
        SaveGameCommand = saveGameCommand;
        LaunchGameCommand = launchGameCommand;
        SaveGeneralCommand = saveGeneralCommand;
        ExportSrmManifestCommand = new AsyncRelayCommand(ExportSrmManifestAsync);
        updateFirmwareCommand = new AsyncRelayCommand(UpdateFirmwareAsync);
        UpdateFirmwareCommand = updateFirmwareCommand;
        CheckForUpdateCommand = new AsyncRelayCommand(CheckForUpdateAsync);
        OpenAppDataCommand = new AsyncRelayCommand(OpenAppDataAsync);

        ReloadGameIds(launchOptions.ProfileId);

        if (!string.IsNullOrWhiteSpace(launchOptions.ProfileId))
        {
            _ = LaunchGameAsync();
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    public event Action<int>? ExitRequested;

    public ObservableCollection<string> GameIds { get; } = [];
    public ObservableCollection<AppTheme> ThemeOptions { get; } = [AppTheme.System, AppTheme.Light, AppTheme.Dark];
    public ObservableCollection<SettingOption<BridgeInputMode>> InputModeOptions { get; } =
    [
        new(BridgeInputMode.LegacyMouse, "Virtual Mouse"),
        new(BridgeInputMode.SteamInputActions, "Steam Input")
    ];
    public ObservableCollection<SettingOption<BridgeOutputMode>> OutputModeOptions { get; } =
    [
        new(BridgeOutputMode.None, "None"),
        new(BridgeOutputMode.Board, "Physical Mouse")
    ];
    public ICommand NewGameCommand { get; }
    public ICommand SaveGameCommand { get; }
    public ICommand LaunchGameCommand { get; }
    public ICommand SaveGeneralCommand { get; }
    public ICommand ExportSrmManifestCommand { get; }
    public ICommand UpdateFirmwareCommand { get; }
    public ICommand CheckForUpdateCommand { get; }
    public ICommand OpenAppDataCommand { get; }

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
            LoadEditor(value, settings.Games[value]);
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
                RaiseProfileCommandStateChanged();
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
                RaiseProfileCommandStateChanged();
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
                RaiseProfileCommandStateChanged();
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
                RaiseProfileCommandStateChanged();
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
                RaiseProfileCommandStateChanged();
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
                SyncRuntimeProfileIfActive();
                RaiseProfileCommandStateChanged();
            }
        }
    }

    public string ProfileText => string.IsNullOrWhiteSpace(EditTitle) ? EditGameId : EditTitle;
    public string ReceiverProcessesText => ReceiverProcesses.Length == 0 ? "None configured" : string.Join(", ", ReceiverProcesses);
    public string AppDataPath { get; } = AppDataPaths.RootDirectory;

    public string SrmManifestPath
    {
        get => srmManifestPath;
        set
        {
            if (SetProperty(ref srmManifestPath, value))
            {
                saveGeneralCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public string BoardPort
    {
        get => boardPort;
        set
        {
            if (SetProperty(ref boardPort, value))
            {
                saveGeneralCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public BridgeInputMode SelectedInputMode
    {
        get => selectedInputMode;
        set
        {
            if (SetProperty(ref selectedInputMode, value))
            {
                SyncRuntimeProfileIfActive(updateInputMode: true);
                RaiseProfileCommandStateChanged();
            }
        }
    }

    public AppTheme SelectedTheme
    {
        get => selectedTheme;
        set
        {
            if (SetProperty(ref selectedTheme, value))
            {
                applyTheme(value);
                saveGeneralCommand.RaiseCanExecuteChanged();
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
                SyncRuntimeProfileIfActive(updateOutputMode: true);
                RaiseProfileCommandStateChanged();
            }
        }
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
    } = "waiting for mouse input";

    public string InputSourceText
    {
        get;
        private set => SetProperty(ref field, value);
    } = "Legacy mouse observer active.";

    public string OutputTargetText
    {
        get;
        private set => SetProperty(ref field, value);
    } = "Board disconnected";

    public string PointerText => $"dx {lastReport.PointerDeltaX}, dy {lastReport.PointerDeltaY}";
    public string WheelText => $"wheel {lastReport.VerticalWheel}";
    public string MouseButtonsText => lastReport.MouseButtons == MouseButtons.None ? "buttons none" : $"buttons {lastReport.MouseButtons}";
    public string MouseLeftBrush => MouseButtonBrush(MouseButtons.Left);
    public string MouseRightBrush => MouseButtonBrush(MouseButtons.Right);
    public string MouseMiddleBrush => MouseButtonBrush(MouseButtons.Middle);
    public string MouseBackBrush => MouseButtonBrush(MouseButtons.Back);
    public string MouseForwardBrush => MouseButtonBrush(MouseButtons.Forward);
    public string StatusBrush => isForwardingActive ? "SeaGreen" : "Gray";
    public string VersionText => appUpdater.CurrentVersionText;
    public string BoardFirmwareText => teensyFirmwareUpdater.HasBundledFirmware
        ? "Flash the packaged firmware to update the board."
        : "Firmware package is missing.";
    public string ActiveProfileText => string.IsNullOrWhiteSpace(activeProfileId) ? "No active profile" : $"Active profile: {activeProfileId}";
    public string TrayText => string.IsNullOrWhiteSpace(activeProfileId) ? "No profile" : $"Profile: {activeProfileId}";
    public string ProcessText => $"{ActiveProfileText} - PID {Environment.ProcessId}";
    public string WindowTitle => string.IsNullOrWhiteSpace(activeProfileId)
        ? "Steam HID Bridge"
        : $"Steam HID Bridge - {activeProfileId}";

    private string[] ReceiverProcesses => MainWindowText.ParseReceiverProcesses(EditReceiverProcessesText);

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

    private bool HasGeneralChanges()
    {
        return savedTheme != SelectedTheme
            || MainWindowText.ParseBoardPort(savedBoardPort) != MainWindowText.ParseBoardPort(BoardPort)
            || !string.Equals(savedSrmManifestPath, SrmManifestPath.Trim(), StringComparison.Ordinal);
    }

    private void RaiseProfileCommandStateChanged()
    {
        saveGameCommand.RaiseCanExecuteChanged();
        launchGameCommand.RaiseCanExecuteChanged();
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
