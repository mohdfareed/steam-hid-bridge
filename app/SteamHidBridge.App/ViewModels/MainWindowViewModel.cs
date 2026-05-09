using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using SteamHidBridge.App.Infrastructure;
using SteamHidBridge.App.Input;
using SteamHidBridge.App.Profiles;
using SteamHidBridge.App.Startup;
using SteamHidBridge.App.Transport;
using SteamHidBridge.App.Windows;
using SteamHidBridge.Protocol;

namespace SteamHidBridge.App.ViewModels;

public sealed class MainWindowViewModel : INotifyPropertyChanged
{
    private readonly BridgeLaunchOptions launchOptions;
    private readonly BridgeProfile profile;
    private readonly IBridgeTransport transport;
    private readonly IInputSourceStatus inputSourceStatus;
    private readonly IForegroundWindowService foregroundWindowService;
    private readonly DispatcherTimer statusTimer;
    private byte sequence;
    private bool isConnected;
    private bool isForwardingEnabled;
    private bool isTargetForeground = true;
    private string foregroundProcessText = "n/a";
    private string targetGateStatus = "No target gate configured";
    private string steamInputStatusText = "Steam Input not checked";
    private double mouseDeltaX = 12;
    private double mouseDeltaY;
    private double wheelDelta;
    private bool isLeftButtonDown = true;
    private bool isRightButtonDown;
    private bool isMiddleButtonDown;
    private bool isBackButtonDown;
    private bool isForwardButtonDown;
    private bool isLeftControlDown;
    private bool isLeftShiftDown;
    private bool isLeftAltDown;
    private bool isLeftGuiDown;
    private bool isRightControlDown;
    private bool isRightShiftDown;
    private bool isRightAltDown;
    private bool isRightGuiDown;
    private int framesSent;
    private int framesRejected;
    private string lastFrameText = "n/a";
    private KeyboardKeyOption selectedKey;

    public MainWindowViewModel(
        BridgeLaunchOptions launchOptions,
        BridgeProfile profile,
        IBridgeTransport transport,
        IInputSourceStatus inputSourceStatus,
        IForegroundWindowService foregroundWindowService)
    {
        this.launchOptions = launchOptions;
        this.profile = profile;
        this.transport = transport;
        this.inputSourceStatus = inputSourceStatus;
        this.foregroundWindowService = foregroundWindowService;
        KeyOptions = CreateKeyOptions();
        selectedKey = KeyOptions[0];
        ToggleConnectionCommand = new AsyncRelayCommand(ToggleConnectionAsync);
        SendSyntheticInputCommand = new AsyncRelayCommand(SendSyntheticInputAsync, () => CanSendSyntheticInput);
        statusTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(500)
        };
        statusTimer.Tick += (_, _) => RefreshRuntimeStatus();
        statusTimer.Start();

        RefreshRuntimeStatus();
        LogEntries.Add($"{Timestamp()} App ready for profile {profile.DisplayName} using {transport.Name}.");
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<string> LogEntries { get; } = [];
    public IReadOnlyList<KeyboardKeyOption> KeyOptions { get; }
    public ICommand ToggleConnectionCommand { get; }
    public ICommand SendSyntheticInputCommand { get; }

    public string ProfileText => $"{profile.DisplayName} ({launchOptions.ProfileId})";
    public string TargetProcessesText => profile.TargetProcesses.Count == 0
        ? "None configured"
        : string.Join(", ", profile.TargetProcesses);
    public string TargetPort => transport.Name;
    public string ForegroundProcessText
    {
        get => foregroundProcessText;
        private set => SetProperty(ref foregroundProcessText, value);
    }

    public string TargetGateStatus
    {
        get => targetGateStatus;
        private set => SetProperty(ref targetGateStatus, value);
    }

    public string SteamInputStatusText
    {
        get => steamInputStatusText;
        private set => SetProperty(ref steamInputStatusText, value);
    }

    public bool IsConnected
    {
        get => isConnected;
        private set => SetProperty(ref isConnected, value);
    }

    public bool IsForwardingEnabled
    {
        get => isForwardingEnabled;
        set => SetProperty(ref isForwardingEnabled, value);
    }

    public double MouseDeltaX
    {
        get => mouseDeltaX;
        set => SetProperty(ref mouseDeltaX, value);
    }

    public double MouseDeltaY
    {
        get => mouseDeltaY;
        set => SetProperty(ref mouseDeltaY, value);
    }

    public double WheelDelta
    {
        get => wheelDelta;
        set => SetProperty(ref wheelDelta, value);
    }

    public bool IsLeftButtonDown
    {
        get => isLeftButtonDown;
        set => SetProperty(ref isLeftButtonDown, value);
    }

    public bool IsRightButtonDown
    {
        get => isRightButtonDown;
        set => SetProperty(ref isRightButtonDown, value);
    }

    public bool IsMiddleButtonDown
    {
        get => isMiddleButtonDown;
        set => SetProperty(ref isMiddleButtonDown, value);
    }

    public bool IsBackButtonDown
    {
        get => isBackButtonDown;
        set => SetProperty(ref isBackButtonDown, value);
    }

    public bool IsForwardButtonDown
    {
        get => isForwardButtonDown;
        set => SetProperty(ref isForwardButtonDown, value);
    }

    public bool IsLeftControlDown
    {
        get => isLeftControlDown;
        set => SetProperty(ref isLeftControlDown, value);
    }

    public bool IsLeftShiftDown
    {
        get => isLeftShiftDown;
        set => SetProperty(ref isLeftShiftDown, value);
    }

    public bool IsLeftAltDown
    {
        get => isLeftAltDown;
        set => SetProperty(ref isLeftAltDown, value);
    }

    public bool IsLeftGuiDown
    {
        get => isLeftGuiDown;
        set => SetProperty(ref isLeftGuiDown, value);
    }

    public bool IsRightControlDown
    {
        get => isRightControlDown;
        set => SetProperty(ref isRightControlDown, value);
    }

    public bool IsRightShiftDown
    {
        get => isRightShiftDown;
        set => SetProperty(ref isRightShiftDown, value);
    }

    public bool IsRightAltDown
    {
        get => isRightAltDown;
        set => SetProperty(ref isRightAltDown, value);
    }

    public bool IsRightGuiDown
    {
        get => isRightGuiDown;
        set => SetProperty(ref isRightGuiDown, value);
    }

    public KeyboardKeyOption SelectedKey
    {
        get => selectedKey;
        set => SetProperty(ref selectedKey, value);
    }

    public int FramesSent
    {
        get => framesSent;
        private set => SetProperty(ref framesSent, value);
    }

    public int FramesRejected
    {
        get => framesRejected;
        private set => SetProperty(ref framesRejected, value);
    }

    public string LastFrameText
    {
        get => lastFrameText;
        private set => SetProperty(ref lastFrameText, value);
    }

    public string ConnectionStatus => IsConnected ? "Connected" : "Disconnected";
    public string ConnectButtonText => IsConnected ? "Disconnect" : "Connect";
    public Brush StatusBrush => IsConnected ? Brushes.SeaGreen : Brushes.Gray;
    public bool CanSendSyntheticInput => IsConnected && IsForwardingEnabled && IsTargetGateOpen;
    private bool IsTargetGateOpen => profile.TargetProcesses.Count == 0 || isTargetForeground;

    private async Task ToggleConnectionAsync()
    {
        if (IsConnected)
        {
            await transport.DisconnectAsync(CancellationToken.None);
            IsForwardingEnabled = false;
            IsConnected = false;
            AddLog("Disconnected.");
        }
        else
        {
            await transport.ConnectAsync(CancellationToken.None);
            IsConnected = true;
            AddLog("Connected to loopback transport.");
        }

        RefreshConnectionDerivedProperties();
    }

    private async Task SendSyntheticInputAsync()
    {
        if (!CanSendSyntheticInput)
        {
            AddLog("synthetic report blocked by connection, forwarding, or target gate");
            return;
        }

        var report = new HidInputReport(
            PointerDeltaX: ClampToInt16(MouseDeltaX),
            PointerDeltaY: ClampToInt16(MouseDeltaY),
            VerticalWheel: ClampToSByte(WheelDelta),
            MouseButtons: CreateMouseButtons(),
            KeyboardModifiers: CreateKeyboardModifiers(),
            KeyboardUsageId: SelectedKey.UsageId);

        byte[] payload = new byte[HidInputReport.WireSize];
        report.WriteTo(payload);

        var frame = new BridgeFrame(BridgeCommand.HidInput, sequence++, payload);
        BridgeTransportResult result = await transport.SendAsync(frame, CancellationToken.None);

        LastFrameText = $"{result.ByteCount} bytes";
        if (result.Accepted)
        {
            FramesSent++;
        }
        else
        {
            FramesRejected++;
        }

        AddLog($"{(result.Accepted ? "accepted" : "rejected")} seq={frame.Sequence} mouse=0x{(ushort)report.MouseButtons:X2} key=0x{report.KeyboardUsageId:X2}; {result.Detail}");
    }

    private MouseButtons CreateMouseButtons()
    {
        MouseButtons buttons = MouseButtons.None;

        if (IsLeftButtonDown)
        {
            buttons |= MouseButtons.Left;
        }

        if (IsRightButtonDown)
        {
            buttons |= MouseButtons.Right;
        }

        if (IsMiddleButtonDown)
        {
            buttons |= MouseButtons.Middle;
        }

        if (IsBackButtonDown)
        {
            buttons |= MouseButtons.Back;
        }

        if (IsForwardButtonDown)
        {
            buttons |= MouseButtons.Forward;
        }

        return buttons;
    }

    private KeyboardModifiers CreateKeyboardModifiers()
    {
        KeyboardModifiers modifiers = KeyboardModifiers.None;

        if (IsLeftControlDown)
        {
            modifiers |= KeyboardModifiers.LeftControl;
        }

        if (IsLeftShiftDown)
        {
            modifiers |= KeyboardModifiers.LeftShift;
        }

        if (IsLeftAltDown)
        {
            modifiers |= KeyboardModifiers.LeftAlt;
        }

        if (IsLeftGuiDown)
        {
            modifiers |= KeyboardModifiers.LeftGui;
        }

        if (IsRightControlDown)
        {
            modifiers |= KeyboardModifiers.RightControl;
        }

        if (IsRightShiftDown)
        {
            modifiers |= KeyboardModifiers.RightShift;
        }

        if (IsRightAltDown)
        {
            modifiers |= KeyboardModifiers.RightAlt;
        }

        if (IsRightGuiDown)
        {
            modifiers |= KeyboardModifiers.RightGui;
        }

        return modifiers;
    }

    private void RefreshRuntimeStatus()
    {
        InputSourceStatus inputStatus = inputSourceStatus.GetStatus();
        SteamInputStatusText = inputStatus.IsAvailable
            ? $"Available: {inputStatus.Detail}"
            : inputStatus.Detail;

        ForegroundWindowSnapshot foregroundWindow = foregroundWindowService.GetForegroundWindow();
        ForegroundProcessText = foregroundWindow.ProcessName is null
            ? "n/a"
            : $"{foregroundWindow.ProcessName} ({foregroundWindow.ProcessId})";

        bool targetForeground = IsTargetProcess(foregroundWindow.ProcessName);
        if (targetForeground != isTargetForeground)
        {
            isTargetForeground = targetForeground;
            OnPropertyChanged(nameof(CanSendSyntheticInput));
            (SendSyntheticInputCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
        }

        if (profile.TargetProcesses.Count == 0)
        {
            TargetGateStatus = "No target gate configured";
            return;
        }

        TargetGateStatus = targetForeground
            ? "Target foreground"
            : "Waiting for target foreground";

        if (profile.AutoEnableWhenForeground && IsConnected && IsForwardingEnabled != targetForeground)
        {
            IsForwardingEnabled = targetForeground;
        }
    }

    private bool IsTargetProcess(string? processName)
    {
        if (processName is null || profile.TargetProcesses.Count == 0)
        {
            return profile.TargetProcesses.Count == 0;
        }

        foreach (string targetProcess in profile.TargetProcesses)
        {
            if (string.Equals(processName, targetProcess, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private void RefreshConnectionDerivedProperties()
    {
        OnPropertyChanged(nameof(ConnectionStatus));
        OnPropertyChanged(nameof(ConnectButtonText));
        OnPropertyChanged(nameof(StatusBrush));
        OnPropertyChanged(nameof(CanSendSyntheticInput));
        (SendSyntheticInputCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
    }

    private void AddLog(string message)
    {
        LogEntries.Insert(0, $"{Timestamp()} {message}");
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
        if (propertyName is nameof(IsConnected) or nameof(IsForwardingEnabled))
        {
            OnPropertyChanged(nameof(CanSendSyntheticInput));
            (SendSyntheticInputCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
        }

        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private static short ClampToInt16(double value)
    {
        return (short)Math.Clamp(Math.Round(value), short.MinValue, short.MaxValue);
    }

    private static sbyte ClampToSByte(double value)
    {
        return (sbyte)Math.Clamp(Math.Round(value), sbyte.MinValue, sbyte.MaxValue);
    }

    private static IReadOnlyList<KeyboardKeyOption> CreateKeyOptions()
    {
        return
        [
            new("None", 0x00),
            new("A", 0x04),
            new("B", 0x05),
            new("C", 0x06),
            new("D", 0x07),
            new("E", 0x08),
            new("F", 0x09),
            new("G", 0x0A),
            new("H", 0x0B),
            new("I", 0x0C),
            new("J", 0x0D),
            new("K", 0x0E),
            new("L", 0x0F),
            new("M", 0x10),
            new("N", 0x11),
            new("O", 0x12),
            new("P", 0x13),
            new("Q", 0x14),
            new("R", 0x15),
            new("S", 0x16),
            new("T", 0x17),
            new("U", 0x18),
            new("V", 0x19),
            new("W", 0x1A),
            new("X", 0x1B),
            new("Y", 0x1C),
            new("Z", 0x1D),
            new("1", 0x1E),
            new("2", 0x1F),
            new("3", 0x20),
            new("4", 0x21),
            new("5", 0x22),
            new("6", 0x23),
            new("7", 0x24),
            new("8", 0x25),
            new("9", 0x26),
            new("0", 0x27),
            new("Enter", 0x28),
            new("Escape", 0x29),
            new("Backspace", 0x2A),
            new("Tab", 0x2B),
            new("Space", 0x2C),
            new("Minus", 0x2D),
            new("Equals", 0x2E),
            new("Left bracket", 0x2F),
            new("Right bracket", 0x30),
            new("Backslash", 0x31),
            new("Semicolon", 0x33),
            new("Apostrophe", 0x34),
            new("Grave", 0x35),
            new("Comma", 0x36),
            new("Period", 0x37),
            new("Slash", 0x38),
            new("Caps Lock", 0x39),
            new("F1", 0x3A),
            new("F2", 0x3B),
            new("F3", 0x3C),
            new("F4", 0x3D),
            new("F5", 0x3E),
            new("F6", 0x3F),
            new("F7", 0x40),
            new("F8", 0x41),
            new("F9", 0x42),
            new("F10", 0x43),
            new("F11", 0x44),
            new("F12", 0x45),
            new("Print Screen", 0x46),
            new("Scroll Lock", 0x47),
            new("Pause", 0x48),
            new("Insert", 0x49),
            new("Home", 0x4A),
            new("Page Up", 0x4B),
            new("Delete", 0x4C),
            new("End", 0x4D),
            new("Page Down", 0x4E),
            new("Right Arrow", 0x4F),
            new("Left Arrow", 0x50),
            new("Down Arrow", 0x51),
            new("Up Arrow", 0x52),
            new("Num Lock", 0x53),
            new("Keypad Slash", 0x54),
            new("Keypad Asterisk", 0x55),
            new("Keypad Minus", 0x56),
            new("Keypad Plus", 0x57),
            new("Keypad Enter", 0x58),
            new("Keypad 1", 0x59),
            new("Keypad 2", 0x5A),
            new("Keypad 3", 0x5B),
            new("Keypad 4", 0x5C),
            new("Keypad 5", 0x5D),
            new("Keypad 6", 0x5E),
            new("Keypad 7", 0x5F),
            new("Keypad 8", 0x60),
            new("Keypad 9", 0x61),
            new("Keypad 0", 0x62),
            new("Keypad Decimal", 0x63),
            new("Non-US Backslash", 0x64),
            new("Application", 0x65),
            new("Power", 0x66),
            new("Keypad Equals", 0x67),
            new("F13", 0x68),
            new("F14", 0x69),
            new("F15", 0x6A),
            new("F16", 0x6B),
            new("F17", 0x6C),
            new("F18", 0x6D),
            new("F19", 0x6E),
            new("F20", 0x6F),
            new("F21", 0x70),
            new("F22", 0x71),
            new("F23", 0x72),
            new("F24", 0x73),
            new("Execute", 0x74),
            new("Help", 0x75),
            new("Menu", 0x76),
            new("Select", 0x77),
            new("Stop", 0x78),
            new("Again", 0x79),
            new("Undo", 0x7A),
            new("Cut", 0x7B),
            new("Copy", 0x7C),
            new("Paste", 0x7D),
            new("Find", 0x7E),
            new("Mute", 0x7F),
            new("Volume Up", 0x80),
            new("Volume Down", 0x81),
            new("Locking Caps Lock", 0x82),
            new("Locking Num Lock", 0x83),
            new("Locking Scroll Lock", 0x84),
            new("Keypad Comma", 0x85),
            new("Keypad Equal Sign", 0x86),
            new("International 1", 0x87),
            new("International 2", 0x88),
            new("International 3", 0x89),
            new("International 4", 0x8A),
            new("International 5", 0x8B),
            new("International 6", 0x8C),
            new("International 7", 0x8D),
            new("International 8", 0x8E),
            new("International 9", 0x8F),
            new("Lang 1", 0x90),
            new("Lang 2", 0x91),
            new("Lang 3", 0x92),
            new("Lang 4", 0x93),
            new("Lang 5", 0x94),
            new("Lang 6", 0x95),
            new("Lang 7", 0x96),
            new("Lang 8", 0x97),
            new("Lang 9", 0x98),
            new("Alternate Erase", 0x99),
            new("SysReq", 0x9A),
            new("Cancel", 0x9B),
            new("Clear", 0x9C),
            new("Prior", 0x9D),
            new("Return", 0x9E),
            new("Separator", 0x9F),
            new("Out", 0xA0),
            new("Oper", 0xA1),
            new("Clear Again", 0xA2),
            new("CrSel Props", 0xA3),
            new("ExSel", 0xA4)
        ];
    }

    private static string Timestamp()
    {
        return DateTimeOffset.Now.ToString("HH:mm:ss.fff");
    }
}
