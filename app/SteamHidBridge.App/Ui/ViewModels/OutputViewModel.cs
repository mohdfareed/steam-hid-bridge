using SteamHidBridge.App.Configuration;
using SteamHidBridge.App.Core;
using SteamHidBridge.Protocol;

namespace SteamHidBridge.App.Ui.ViewModels;

internal sealed class OutputViewModel : ObservableObject
{
    private bool isForwardingActive;
    private HidInputReport lastReport;

    public string ForwardingStatus
    {
        get;
        private set => SetProperty(ref field, value);
    } = "Forwarding off";

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

    public void ApplyRuntimeStatus(BridgeSessionStatus status)
    {
        isForwardingActive = status.ForwardingEnabled;
        ForwardingStatus = status.ForwardingEnabled ? "Forwarding on" : "Forwarding off";
        OutputTargetText = FormatOutputTarget(status.OutputMode, status.BoardOutput);
        OnPropertyChanged(nameof(StatusBrush));
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

    private string MouseButtonBrush(MouseButtons button)
    {
        return lastReport.MouseButtons.HasFlag(button) ? "SeaGreen" : "White";
    }

    private static string FormatOutputTarget(BridgeOutputMode outputMode, BoardOutputStatus boardStatus)
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
                    _ => "Board error"
                },
                OutputConnectionState.Disconnected => string.IsNullOrWhiteSpace(boardStatus.Endpoint)
                    ? "Board disconnected: no serial port opened"
                    : $"Board disconnected: {boardStatus.Endpoint}",
                OutputConnectionState.Idle => "Board idle",
                _ => "Board idle"
            },
            _ => "Unknown output"
        };
    }
}
