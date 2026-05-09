using System;
using SteamHidBridge.App.Windows;
using SteamHidBridge.Protocol;

namespace SteamHidBridge.App.ViewModels;

public sealed partial class MainWindowViewModel
{
    private void RefreshRuntimeStatus()
    {
        if (isStoppingLaunchedProcesses)
        {
            return;
        }

        string[] receiverProcesses = ReceiverProcesses;
        string foregroundProcess = WindowsRuntime.GetForegroundProcessName();

        if (receiverProcesses.Length == 0)
        {
            ForwardingStatus = "Forwarding off";
            UpdateForwardingActive(false);
            UpdateSteamInputConfigForce(false);
            if (launchOptions.LaunchGame && launchedProcess is not null && launchedProcessExited)
            {
                SetActivity("Launched process exited; closing bridge.");
                RequestExit(0);
            }

            return;
        }

        bool receiverRunning = WindowsRuntime.IsAnyProcessRunning(receiverProcesses);
        if (receiverRunning)
        {
            hasSeenReceiverProcess = true;
        }

        if (launchOptions.LaunchGame && hasSeenReceiverProcess && !receiverRunning)
        {
            SetActivity("Receiver process exited; closing bridge.");
            RequestExit(0);
            return;
        }

        bool receiverForeground = receiverRunning && IsReceiverProcess(foregroundProcess);
        bool shouldForward = receiverRunning && receiverForeground;
        ForwardingStatus = shouldForward ? "Forwarding on" : "Forwarding off";
        UpdateForwardingActive(shouldForward);
        UpdateSteamInputConfigForce(shouldForward);

        if (Steam.SteamInputReader.TryReadLatest(out HidInputReport report))
        {
            PreviewOutput(report);
        }
    }

    private void UpdateForwardingActive(bool value)
    {
        if (isForwardingActive == value)
        {
            return;
        }

        isForwardingActive = value;
        OnPropertyChanged(nameof(StatusBrush));
    }

    private void UpdateSteamInputConfigForce(bool shouldForce)
    {
        if (steamInputConfigForcer.TrySetForced(shouldForce, out string message) && !string.IsNullOrWhiteSpace(message))
        {
            SetActivity(message);
        }
    }

    private static string[] ParseReceiverProcesses(string value)
    {
        return value.Split(
            [',', ';', '\r', '\n'],
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    private bool IsReceiverProcess(string processName)
    {
        if (string.IsNullOrWhiteSpace(processName))
        {
            return false;
        }

        foreach (string receiverProcess in ReceiverProcesses)
        {
            if (string.Equals(receiverProcess, processName, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
