using System;
using SteamHidBridge.App.Steam;
using SteamHidBridge.App.Windows;

namespace SteamHidBridge.App.Runtime;

public sealed class ForwardingGate(Action<string, bool> setActivity) : IDisposable
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
            return new ForwardingGateResult("Forwarding off", false);
        }

        bool receiverRunning = WindowsRuntime.IsAnyProcessRunning(receivers);
        if (receiverRunning)
        {
            hasSeenReceiverProcess = true;
        }

        if (launchMode && hasSeenReceiverProcess && !receiverRunning)
        {
            SetForwarding(false);
            return new ForwardingGateResult("Forwarding off", true);
        }

        bool shouldForward = receiverRunning && IsReceiverProcess(WindowsRuntime.GetForegroundProcessName(), receivers);
        SetForwarding(shouldForward);
        return new ForwardingGateResult(shouldForward ? "Forwarding on" : "Forwarding off", false);
    }

    public void Dispose()
    {
        steamInputConfigForcer.Reset();
    }

    private void SetForwarding(bool value)
    {
        IsForwarding = value;
        if (steamInputConfigForcer.TrySetForced(value, out string message) && !string.IsNullOrWhiteSpace(message))
        {
            setActivity(message, false);
        }
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

public sealed record ForwardingGateResult(string StatusText, bool ReceiverExited);
