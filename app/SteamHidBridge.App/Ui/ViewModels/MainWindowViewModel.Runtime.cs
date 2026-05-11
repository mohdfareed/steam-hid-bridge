using SteamHidBridge.App.Core;

namespace SteamHidBridge.App.Ui.ViewModels;

internal sealed partial class MainWindowViewModel
{
    private void ApplyRuntimeStatus(BridgeRuntimeStatus status)
    {
        isForwardingActive = status.ForwardingEnabled;
        ForwardingStatus = status.ForwardingEnabled ? "Forwarding on" : "Forwarding off";
        InputLoopText = MainWindowText.FormatInputLoop(status);
        InputSourceText = MainWindowText.FormatInputSource(status);
        OutputTargetText = MainWindowText.FormatOutputTarget(status);
        OnPropertyChanged(nameof(StatusBrush));
    }
}
