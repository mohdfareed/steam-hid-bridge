using System;
using System.Threading;
using System.Windows.Threading;
using SteamHidBridge.App.Configuration;
using SteamHidBridge.App.Core;
using SteamHidBridge.App.Platform.Windows;
using SteamHidBridge.Protocol;

namespace SteamHidBridge.App.Ui.ViewModels;

internal sealed class OutputViewModel(
    Action<MouseButtons> applyBoardButtons,
    Action<short, short, sbyte> sendBoardFrame) : ObservableObject
{
    private readonly Lock inputLock = new();
    private readonly Lock outputLock = new();
    private Dispatcher? inputDispatcher;
    private Dispatcher? outputDispatcher;
    private RawMouseObservation latestInputObservation;
    private RawMouseObservation latestOutputObservation;
    private MouseInputFrame lastInputFrame;
    private MouseInputFrame lastOutputFrame;
    private bool inputScheduled;
    private bool outputScheduled;
    private bool manualLeft;
    private bool manualRight;
    private bool manualMiddle;
    private bool manualBack;
    private bool manualForward;

    public string ForwardingText
    {
        get;
        private set => SetProperty(ref field, value);
    } = "off";

    public string OutputTargetText
    {
        get;
        private set => SetProperty(ref field, value);
    } = "Board disconnected";

    public string InputDeviceText
    {
        get;
        private set => SetProperty(ref field, value);
    } = "No input yet";

    public string InputPointerText => $"dx {lastInputFrame.PointerDeltaX}, dy {lastInputFrame.PointerDeltaY}";
    public string InputWheelText => $"wheel {lastInputFrame.VerticalWheel}";

    public string OutputDeviceText
    {
        get;
        private set => SetProperty(ref field, value);
    } = "No observed raw output yet";

    public string OutputPointerText => $"dx {lastOutputFrame.PointerDeltaX}, dy {lastOutputFrame.PointerDeltaY}";
    public string OutputWheelText => $"wheel {lastOutputFrame.VerticalWheel}";
    public string InputLeftBrush => MouseButtonBrush(lastInputFrame.Buttons, MouseButtons.Left);
    public string InputRightBrush => MouseButtonBrush(lastInputFrame.Buttons, MouseButtons.Right);
    public string InputMiddleBrush => MouseButtonBrush(lastInputFrame.Buttons, MouseButtons.Middle);
    public string InputBackBrush => MouseButtonBrush(lastInputFrame.Buttons, MouseButtons.Back);
    public string InputForwardBrush => MouseButtonBrush(lastInputFrame.Buttons, MouseButtons.Forward);
    public string OutputLeftBrush => MouseButtonBrush(lastOutputFrame.Buttons, MouseButtons.Left);
    public string OutputRightBrush => MouseButtonBrush(lastOutputFrame.Buttons, MouseButtons.Right);
    public string OutputMiddleBrush => MouseButtonBrush(lastOutputFrame.Buttons, MouseButtons.Middle);
    public string OutputBackBrush => MouseButtonBrush(lastOutputFrame.Buttons, MouseButtons.Back);
    public string OutputForwardBrush => MouseButtonBrush(lastOutputFrame.Buttons, MouseButtons.Forward);
    public string InputSelectionText
    {
        get;
        private set => SetProperty(ref field, value);
    } = "Automatic";
    public bool ManualLeft
    {
        get => manualLeft;
        set
        {
            if (SetProperty(ref manualLeft, value))
            {
                ApplyManualButtons();
            }
        }
    }

    public bool ManualRight
    {
        get => manualRight;
        set
        {
            if (SetProperty(ref manualRight, value))
            {
                ApplyManualButtons();
            }
        }
    }

    public bool ManualMiddle
    {
        get => manualMiddle;
        set
        {
            if (SetProperty(ref manualMiddle, value))
            {
                ApplyManualButtons();
            }
        }
    }

    public bool ManualBack
    {
        get => manualBack;
        set
        {
            if (SetProperty(ref manualBack, value))
            {
                ApplyManualButtons();
            }
        }
    }

    public bool ManualForward
    {
        get => manualForward;
        set
        {
            if (SetProperty(ref manualForward, value))
            {
                ApplyManualButtons();
            }
        }
    }

    public event Action? PinNextInputRequested;
    public event Action? ClearInputPinRequested;

    public void ApplyRuntimeStatus(BridgeSessionStatus status)
    {
        ForwardingText = status.ForwardingEnabled ? "on" : "off";
        OutputTargetText = FormatOutputTarget(status.OutputMode, status.BoardOutput, status.ViiperOutput);
    }

    public void QueueInputObservation(Dispatcher dispatcher, RawMouseObservation observation)
    {
        bool schedule;
        lock (inputLock)
        {
            inputDispatcher = dispatcher;
            latestInputObservation = observation;
            schedule = !inputScheduled;
            if (schedule)
            {
                inputScheduled = true;
            }
        }

        if (schedule)
        {
            _ = dispatcher.BeginInvoke(FlushInput, DispatcherPriority.Background);
        }
    }

    public void QueueOutputObservation(Dispatcher dispatcher, RawMouseObservation observation)
    {
        bool schedule;
        lock (outputLock)
        {
            outputDispatcher = dispatcher;
            latestOutputObservation = observation;
            schedule = !outputScheduled;
            if (schedule)
            {
                outputScheduled = true;
            }
        }

        if (schedule)
        {
            _ = dispatcher.BeginInvoke(FlushOutput, DispatcherPriority.Background);
        }
    }

    public void MoveBoard(short deltaX, short deltaY)
    {
        sendBoardFrame(deltaX, deltaY, 0);
    }

    public void WheelBoard(sbyte wheel)
    {
        sendBoardFrame(0, 0, wheel);
    }

    public void ClearManualButtons()
    {
        manualLeft = false;
        manualRight = false;
        manualMiddle = false;
        manualBack = false;
        manualForward = false;
        OnPropertyChanged(nameof(ManualLeft));
        OnPropertyChanged(nameof(ManualRight));
        OnPropertyChanged(nameof(ManualMiddle));
        OnPropertyChanged(nameof(ManualBack));
        OnPropertyChanged(nameof(ManualForward));
        ApplyManualButtons();
    }

    public void RequestPinNextInput()
    {
        InputSelectionText = "Waiting for next input device";
        PinNextInputRequested?.Invoke();
    }

    public void ClearInputPin()
    {
        InputSelectionText = "Automatic";
        ClearInputPinRequested?.Invoke();
    }

    public void SetPinnedInputDevice(string deviceName)
    {
        InputSelectionText = $"Pinned: {deviceName}";
    }

    private void FlushInput()
    {
        Dispatcher? dispatcher;
        RawMouseObservation observation;
        lock (inputLock)
        {
            dispatcher = inputDispatcher;
            observation = latestInputObservation;
        }

        ApplyInputObservation(observation);

        bool reschedule;
        lock (inputLock)
        {
            reschedule = !observation.Equals(latestInputObservation);
            inputScheduled = reschedule;
        }

        if (reschedule)
        {
            _ = dispatcher?.BeginInvoke(FlushInput, DispatcherPriority.Background);
        }
    }

    private void FlushOutput()
    {
        Dispatcher? dispatcher;
        RawMouseObservation observation;
        lock (outputLock)
        {
            dispatcher = outputDispatcher;
            observation = latestOutputObservation;
        }

        ApplyOutputObservation(observation);

        bool reschedule;
        lock (outputLock)
        {
            reschedule = !observation.Equals(latestOutputObservation);
            outputScheduled = reschedule;
        }

        if (reschedule)
        {
            _ = dispatcher?.BeginInvoke(FlushOutput, DispatcherPriority.Background);
        }
    }

    private void ApplyInputObservation(RawMouseObservation observation)
    {
        lastInputFrame = observation.Frame;
        InputDeviceText = observation.DeviceName;
        OnPropertyChanged(nameof(InputPointerText));
        OnPropertyChanged(nameof(InputWheelText));
        OnPropertyChanged(nameof(InputLeftBrush));
        OnPropertyChanged(nameof(InputRightBrush));
        OnPropertyChanged(nameof(InputMiddleBrush));
        OnPropertyChanged(nameof(InputBackBrush));
        OnPropertyChanged(nameof(InputForwardBrush));
    }

    private void ApplyOutputObservation(RawMouseObservation observation)
    {
        lastOutputFrame = observation.Frame;
        OutputDeviceText = observation.DeviceName;
        OnPropertyChanged(nameof(OutputPointerText));
        OnPropertyChanged(nameof(OutputWheelText));
        OnPropertyChanged(nameof(OutputLeftBrush));
        OnPropertyChanged(nameof(OutputRightBrush));
        OnPropertyChanged(nameof(OutputMiddleBrush));
        OnPropertyChanged(nameof(OutputBackBrush));
        OnPropertyChanged(nameof(OutputForwardBrush));
    }

    private static string MouseButtonBrush(MouseButtons buttons, MouseButtons button)
    {
        return buttons.HasFlag(button) ? "SeaGreen" : "#FF9CA3AF";
    }

    private void ApplyManualButtons()
    {
        MouseButtons buttons = MouseButtons.None;
        if (manualLeft)
        {
            buttons |= MouseButtons.Left;
        }

        if (manualRight)
        {
            buttons |= MouseButtons.Right;
        }

        if (manualMiddle)
        {
            buttons |= MouseButtons.Middle;
        }

        if (manualBack)
        {
            buttons |= MouseButtons.Back;
        }

        if (manualForward)
        {
            buttons |= MouseButtons.Forward;
        }

        applyBoardButtons(buttons);
    }

    private static string FormatOutputTarget(BridgeOutputMode outputMode, OutputStatus boardStatus, OutputStatus viiperStatus)
    {
        return outputMode switch
        {
            BridgeOutputMode.None => "Visualize only",
            BridgeOutputMode.Board => boardStatus.State switch
            {
                OutputConnectionState.Connected => $"Board connected: {boardStatus.Endpoint}",
                OutputConnectionState.Error => boardStatus.Error switch
                {
                    OutputError.None => "Board error",
                    OutputError.FrameEncodeFailed => "Board error: frame encode failed",
                    OutputError.WriteFailed => string.IsNullOrWhiteSpace(boardStatus.Endpoint)
                        ? "Board error: write failed"
                        : $"Board error: {boardStatus.Endpoint}",
                    OutputError.ConnectFailed => "Board error",
                    _ => "Board error"
                },
                OutputConnectionState.Disconnected => string.IsNullOrWhiteSpace(boardStatus.Endpoint)
                    ? "Board disconnected: no serial port opened"
                    : $"Board disconnected: {boardStatus.Endpoint}",
                OutputConnectionState.Idle => "Board idle",
                _ => "Board idle"
            },
            BridgeOutputMode.Viiper => viiperStatus.State switch
            {
                OutputConnectionState.Connected => $"Virtual mouse connected at {viiperStatus.Endpoint}",
                OutputConnectionState.Error => viiperStatus.Error switch
                {
                    OutputError.ConnectFailed => $"Virtual mouse unavailable: {viiperStatus.Endpoint}",
                    OutputError.WriteFailed => $"Virtual mouse write failed: {viiperStatus.Endpoint}",
                    OutputError.None => "Virtual mouse error",
                    OutputError.FrameEncodeFailed => "Virtual mouse error",
                    _ => "Virtual mouse error"
                },
                OutputConnectionState.Disconnected => $"Virtual mouse disconnected: {viiperStatus.Endpoint}",
                OutputConnectionState.Idle => "Virtual mouse idle",
                _ => "Virtual mouse idle"
            },
            _ => "Unknown output"
        };
    }
}
