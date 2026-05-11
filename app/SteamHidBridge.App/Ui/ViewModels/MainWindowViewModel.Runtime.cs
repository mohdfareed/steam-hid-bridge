using System;
using System.Linq;
using SteamHidBridge.App.Configuration;
using SteamHidBridge.App.Core.Input;
using SteamHidBridge.App.Core.Runtime;

namespace SteamHidBridge.App.Ui.ViewModels;

internal sealed partial class MainWindowViewModel
{
    private void ApplyRuntimeStatus(BridgeRuntimeStatus status)
    {
        isForwardingActive = status.ForwardingEnabled;
        ForwardingStatus = status.ForwardingEnabled ? "Forwarding on" : "Forwarding off";
        InputLoopText = status.Statistics.EventCount == 0
            ? "waiting for mouse input"
            : $"events {status.Statistics.EventsPerSecond:F0}/s, preview {status.Statistics.PreviewFramesPerSecond:F0}/s";
        InputSourceText = FormatInputSource(status);
        OutputTargetText = status.Outputs.Length == 0
            ? "No physical output configured."
            : string.Join("; ", status.Outputs.Select(FormatOutputStatus));
        OnPropertyChanged(nameof(StatusBrush));
    }

    private static string[] ParseReceiverProcesses(string value)
    {
        return value.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    private static string FormatInputSource(BridgeRuntimeStatus status)
    {
        return status.InputMode switch
        {
            BridgeInputMode.LegacyMouse => "Virtual mouse observer active.",
            BridgeInputMode.SteamInputActions => FormatSteamInputStatus(status.InputStatus),
            _ => "Input inactive"
        };
    }

    private static string FormatOutputStatus(OutputStatus status)
    {
        return status.Mode switch
        {
            BridgeOutputMode.None => "Visualize only",
            BridgeOutputMode.Board => FormatBoardStatus(status),
            _ => "Unknown output"
        };
    }

    private static string FormatBoardStatus(OutputStatus status)
    {
        return status.State switch
        {
            OutputConnectionState.Connected => $"Board connected: {status.Endpoint}",
            OutputConnectionState.Error => status.Error switch
            {
                OutputError.FrameEncodeFailed => "Board error: frame encode failed",
                OutputError.WriteFailed => string.IsNullOrWhiteSpace(status.Endpoint)
                    ? "Board error: write failed"
                    : $"Board error: {status.Endpoint}",
                OutputError.None => throw new NotImplementedException(),
                OutputError.UnknownMode => throw new NotImplementedException(),
                _ => "Board error",
            },
            OutputConnectionState.Disconnected => string.IsNullOrWhiteSpace(status.Endpoint)
                ? "Board disconnected: no serial port opened"
                : $"Board disconnected: {status.Endpoint}",
            OutputConnectionState.Idle => "Board idle",
            _ => "Board idle"
        };
    }

    private static string FormatSteamInputStatus(InputSourceStatus status)
    {
        return status.State switch
        {
            InputSourceState.Inactive => "Steam Input inactive.",
            InputSourceState.Starting => "Steam Input starting.",
            InputSourceState.Ready when status.ControllerCount > 0 => $"Steam Input ready: {status.ControllerCount} controller(s)",
            InputSourceState.Ready => "Steam Input ready: no controllers.",
            InputSourceState.Error when !string.IsNullOrWhiteSpace(status.Detail) => $"Steam Input unavailable: {status.Detail}",
            InputSourceState.Error => "Steam Input unavailable.",
            _ => "Steam Input inactive."
        };
    }
}
