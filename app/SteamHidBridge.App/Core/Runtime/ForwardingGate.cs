using System;
using SteamHidBridge.App.Platform.Steam;
using SteamHidBridge.App.Platform.Windows;

namespace SteamHidBridge.App.Core.Runtime;

internal readonly record struct ForwardingGateResult(bool IsForwarding, bool ReceiverExited);

internal sealed class ForwardingGate : IDisposable
{
    private readonly SteamInputConfigForcer steamInputConfigForcer = new();
    private bool hasSeenReceiverProcess;

    public bool IsForwarding { get; private set; }

    public void ResetProfile()
    {
        hasSeenReceiverProcess = false;
        SetForwarding(false);
    }

    public ForwardingGateResult Refresh(string[] receivers, bool launchMode)
    {
        if (receivers.Length == 0)
        {
            SetForwarding(false);
            return new ForwardingGateResult(false, false);
        }

        bool receiverRunning = WindowsRuntime.IsAnyProcessRunning(receivers);
        if (receiverRunning)
        {
            hasSeenReceiverProcess = true;
        }

        if (launchMode && hasSeenReceiverProcess && !receiverRunning)
        {
            SetForwarding(false);
            return new ForwardingGateResult(false, true);
        }

        bool shouldForward = receiverRunning && IsReceiverProcess(WindowsRuntime.GetForegroundProcessName(), receivers);
        SetForwarding(shouldForward);
        return new ForwardingGateResult(shouldForward, false);
    }

    public void Dispose()
    {
        steamInputConfigForcer.Reset();
    }

    private void SetForwarding(bool value)
    {
        IsForwarding = value;
        _ = steamInputConfigForcer.TrySetForced(value);
    }

    private static bool IsReceiverProcess(string processName, string[] receivers)
    {
        if (string.IsNullOrWhiteSpace(processName))
        {
            return false;
        }

        foreach (string receiver in receivers)
        {
            if (string.Equals(receiver, processName, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
