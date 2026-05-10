using System;
using SteamHidBridge.App.Runtime;

namespace SteamHidBridge.App.ViewModels;

public sealed partial class MainWindowViewModel
{
    private void ApplyRuntimeStatus(BridgeRuntimeStatus status)
    {
        isForwardingActive = status.ForwardingEnabled;
        ForwardingStatus = status.ForwardingText;
        InputLoopText = status.InputLoopText;
        InputSourceText = status.InputSourceText;
        OnPropertyChanged(nameof(StatusBrush));
    }

    private static string[] ParseReceiverProcesses(string value)
    {
        return value.Split(
            [',', ';', '\r', '\n'],
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }
}
