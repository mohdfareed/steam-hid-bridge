using System.Threading;
using System.Windows.Threading;
using SteamHidBridge.App.Configuration;
using SteamHidBridge.App.Core;
using SteamHidBridge.Protocol;

namespace SteamHidBridge.App.Ui.ViewModels;

internal sealed class OutputViewModel : ObservableObject
{
    private readonly Lock previewLock = new();
    private Dispatcher? previewDispatcher;
    private HidInputReport lastReport;
    private MouseInputFrame latestPreviewFrame;
    private bool previewScheduled;

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

    public string PointerText => $"dx {lastReport.PointerDeltaX}, dy {lastReport.PointerDeltaY}";
    public string WheelText => $"wheel {lastReport.VerticalWheel}";
    public string MouseButtonsText => lastReport.MouseButtons == MouseButtons.None ? "buttons none" : $"buttons {lastReport.MouseButtons}";
    public string LastOutputText
    {
        get;
        private set => SetProperty(ref field, value);
    } = "No output yet";
    public string MouseLeftBrush => MouseButtonBrush(MouseButtons.Left);
    public string MouseRightBrush => MouseButtonBrush(MouseButtons.Right);
    public string MouseMiddleBrush => MouseButtonBrush(MouseButtons.Middle);
    public string MouseBackBrush => MouseButtonBrush(MouseButtons.Back);
    public string MouseForwardBrush => MouseButtonBrush(MouseButtons.Forward);

    public void ApplyRuntimeStatus(BridgeSessionStatus status)
    {
        ForwardingText = status.ForwardingEnabled ? "on" : "off";
        OutputTargetText = FormatOutputTarget(status.OutputMode, status.BoardOutput, status.ViiperOutput);
        LastOutputText = FormatLastOutput(status.OutputMode, status.BoardOutput, status.ViiperOutput);
    }

    public void PreviewMouseInput(MouseInputFrame frame)
    {
        lastReport = new HidInputReport(
            frame.PointerDeltaX,
            frame.PointerDeltaY,
            frame.VerticalWheel,
            frame.Buttons);

        OnPropertyChanged(nameof(PointerText));
        OnPropertyChanged(nameof(WheelText));
        OnPropertyChanged(nameof(MouseButtonsText));
        OnPropertyChanged(nameof(MouseLeftBrush));
        OnPropertyChanged(nameof(MouseRightBrush));
        OnPropertyChanged(nameof(MouseMiddleBrush));
        OnPropertyChanged(nameof(MouseBackBrush));
        OnPropertyChanged(nameof(MouseForwardBrush));
    }

    public void QueuePreviewMouseInput(Dispatcher dispatcher, MouseInputFrame frame)
    {
        bool schedule;
        lock (previewLock)
        {
            previewDispatcher = dispatcher;
            latestPreviewFrame = frame;
            schedule = !previewScheduled;
            if (schedule)
            {
                previewScheduled = true;
            }
        }

        if (schedule)
        {
            _ = dispatcher.BeginInvoke(FlushPreview, DispatcherPriority.Background);
        }
    }

    private void FlushPreview()
    {
        Dispatcher? dispatcher;
        MouseInputFrame frame;
        lock (previewLock)
        {
            dispatcher = previewDispatcher;
            frame = latestPreviewFrame;
        }

        PreviewMouseInput(frame);

        bool reschedule;
        lock (previewLock)
        {
            if (frame.Equals(latestPreviewFrame))
            {
                previewScheduled = false;
                reschedule = false;
            }
            else
            {
                reschedule = true;
            }
        }

        if (reschedule)
        {
            _ = dispatcher?.BeginInvoke(FlushPreview, DispatcherPriority.Background);
        }
    }

    private string MouseButtonBrush(MouseButtons button)
    {
        return lastReport.MouseButtons.HasFlag(button) ? "SeaGreen" : "White";
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

    private static string FormatLastOutput(BridgeOutputMode outputMode, OutputStatus boardStatus, OutputStatus viiperStatus)
    {
        MouseInputFrame? frame = outputMode switch
        {
            BridgeOutputMode.Board => boardStatus.LastFrame,
            BridgeOutputMode.Viiper => viiperStatus.LastFrame,
            BridgeOutputMode.None => null,
            _ => null
        };

        return frame is MouseInputFrame value
            ? $"dx {value.PointerDeltaX}, dy {value.PointerDeltaY}, wheel {value.VerticalWheel}, buttons {(value.Buttons == MouseButtons.None ? "none" : value.Buttons)}"
            : "No output yet";
    }
}
