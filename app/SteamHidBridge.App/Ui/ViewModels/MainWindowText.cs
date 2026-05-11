using System;
using SteamHidBridge.App.Configuration;
using SteamHidBridge.App.Core;

namespace SteamHidBridge.App.Ui.ViewModels;

internal static class MainWindowText
{
    public static string[] ParseReceiverProcesses(string value)
    {
        return value.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    public static int? ParseBoardPort(string value)
    {
        string trimmed = value.Trim();
        return string.IsNullOrWhiteSpace(trimmed) ? null : int.TryParse(trimmed, out int portNumber) && portNumber > 0 ? portNumber : null;
    }

    public static string FormatInputLoop(BridgeRuntimeStatus status)
    {
        return status.InputMode == BridgeInputMode.SteamInputActions && status.InputStatus.State == InputSourceState.Ready
            ? "Steam Input ready."
            : "Waiting for mouse input.";
    }

    public static string FormatInputSource(BridgeRuntimeStatus status)
    {
        return status.InputMode switch
        {
            BridgeInputMode.LegacyMouse => "Virtual mouse observer active.",
            BridgeInputMode.SteamInputActions => FormatSteamInputStatus(status.InputStatus),
            _ => "Input inactive"
        };
    }

    public static string FormatOutputTarget(BridgeRuntimeStatus status)
    {
        return status.OutputMode switch
        {
            BridgeOutputMode.None => "Visualize only",
            BridgeOutputMode.Board => FormatBoardStatus(status.BoardOutput),
            _ => "Unknown output"
        };
    }

    private static string FormatBoardStatus(BoardOutputStatus status)
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
                OutputError.None => "Board error",
                OutputError.UnknownMode => "Board error: unknown output mode",
                _ => "Board error"
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
