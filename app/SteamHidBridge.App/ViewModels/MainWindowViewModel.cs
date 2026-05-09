using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using SteamHidBridge.App.Infrastructure;
using SteamHidBridge.App.Profiles;
using SteamHidBridge.App.Startup;
using SteamHidBridge.App.Steam;
using SteamHidBridge.App.Transport;
using SteamHidBridge.App.Windows;
using SteamHidBridge.Protocol;

namespace SteamHidBridge.App.ViewModels;

public sealed partial class MainWindowViewModel : INotifyPropertyChanged
{
    private readonly BridgeLaunchOptions launchOptions;
    private readonly AppSettingsStore settingsStore;
    private readonly LoopbackBridgeTransport transport;
    private readonly ChildProcessJob childProcessJob = new();
    private readonly DispatcherTimer statusTimer;
    private byte sequence;
    private string selectedGameId = string.Empty;
    private string editGameId = string.Empty;
    private string editTitle = string.Empty;
    private string editExecutable = string.Empty;
    private string editArguments = string.Empty;
    private string editWorkingDirectory = string.Empty;
    private string editReceiverProcessesText = string.Empty;
    private int framesPreviewed;
    private int framesForwarded;
    private int framesBlocked;
    private bool hasSeenReceiverProcess;
    private bool isForwardingActive;
    private bool isStoppingLaunchedProcesses;
    private bool launchedProcessExited = true;
    private Process? launchedProcess;
    private HidInputReport lastReport;

    public MainWindowViewModel(
        BridgeLaunchOptions launchOptions,
        AppSettingsStore settingsStore,
        LoopbackBridgeTransport transport)
    {
        this.launchOptions = launchOptions;
        this.settingsStore = settingsStore;
        this.transport = transport;

        NewGameCommand = new AsyncRelayCommand(NewGameAsync);
        SaveGameCommand = new AsyncRelayCommand(SaveGameAsync);
        LaunchGameCommand = new AsyncRelayCommand(LaunchGameAsync);
        CopySteamRomManagerJsonCommand = new AsyncRelayCommand(CopySteamRomManagerJsonAsync);

        ReloadGameIds(launchOptions.ProfileId);

        statusTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(250)
        };
        statusTimer.Tick += (_, _) => RefreshRuntimeStatus();
        statusTimer.Start();

        RefreshRuntimeStatus();
        AddLog($"Ready. profile={selectedGameId} transport={LoopbackBridgeTransport.Name}");
        AddLog($"Settings: {settingsStore.Path}");

        if (launchOptions.LaunchGame)
        {
            _ = LaunchGameAsync();
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    public event Action<int>? ExitRequested;

    public ObservableCollection<string> GameIds { get; } = [];
    public ObservableCollection<string> LogEntries { get; } = [];
    public ICommand NewGameCommand { get; }
    public ICommand SaveGameCommand { get; }
    public ICommand LaunchGameCommand { get; }
    public ICommand CopySteamRomManagerJsonCommand { get; }

    public string SelectedGameId
    {
        get => selectedGameId;
        set
        {
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
                RefreshRuntimeStatus();
            }
        }
    }

    public string ProfileText => string.IsNullOrWhiteSpace(EditTitle) ? EditGameId : $"{EditTitle} ({EditGameId})";
    public string ReceiverProcessesText => ReceiverProcesses.Length == 0 ? "None configured" : string.Join(", ", ReceiverProcesses);
    public string TransportText { get; } = LoopbackBridgeTransport.Name;
    public string SteamInputStatusText { get; } = SteamInputReader.StatusText;

    public string ReceiverStatus
    {
        get;
        private set => SetProperty(ref field, value);
    } = "No receiver process configured";

    public string ForegroundStatus
    {
        get;
        private set => SetProperty(ref field, value);
    } = "Foreground: n/a";

    public string ForwardingStatus
    {
        get;
        private set => SetProperty(ref field, value);
    } = "Preview only";

    public string LastOutputText
    {
        get;
        private set => SetProperty(ref field, value);
    } = "No output yet";

    public string FramesPreviewedText
    {
        get;
        private set => SetProperty(ref field, value);
    } = "0";

    public string FramesForwardedText
    {
        get;
        private set => SetProperty(ref field, value);
    } = "0";

    public string FramesBlockedText
    {
        get;
        private set => SetProperty(ref field, value);
    } = "0";

    public string PointerText => $"dx {lastReport.PointerDeltaX}, dy {lastReport.PointerDeltaY}";
    public string WheelText => $"wheel {lastReport.VerticalWheel}";
    public string MouseButtonsText => lastReport.MouseButtons == MouseButtons.None ? "buttons none" : $"buttons {lastReport.MouseButtons}";
    public string KeyboardText => $"keyboard modifiers {lastReport.KeyboardModifiers}, usage 0x{lastReport.KeyboardUsageId:X2}";
    public Brush MouseLeftBrush => MouseButtonBrush(MouseButtons.Left);
    public Brush MouseRightBrush => MouseButtonBrush(MouseButtons.Right);
    public Brush MouseMiddleBrush => MouseButtonBrush(MouseButtons.Middle);
    public Brush MouseBackBrush => MouseButtonBrush(MouseButtons.Back);
    public Brush MouseForwardBrush => MouseButtonBrush(MouseButtons.Forward);
    public Brush StatusBrush => isForwardingActive ? Brushes.SeaGreen : Brushes.Gray;

    private string[] ReceiverProcesses => ParseReceiverProcesses(EditReceiverProcessesText);

    private void AddLog(string message)
    {
        LogEntries.Insert(0, $"{DateTimeOffset.Now:HH:mm:ss.fff} {message}");
        while (LogEntries.Count > 200)
        {
            LogEntries.RemoveAt(LogEntries.Count - 1);
        }
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

    private SolidColorBrush MouseButtonBrush(MouseButtons button)
    {
        return lastReport.MouseButtons.HasFlag(button) ? Brushes.SeaGreen : Brushes.White;
    }
}
