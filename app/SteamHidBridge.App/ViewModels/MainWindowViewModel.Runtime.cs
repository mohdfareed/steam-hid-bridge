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

        ForegroundStatus = string.IsNullOrWhiteSpace(foregroundProcess)
            ? "Foreground: n/a"
            : $"Foreground: {foregroundProcess}";

        if (receiverProcesses.Length == 0)
        {
            ReceiverStatus = "No receiver process configured";
            ForwardingStatus = "Preview only";
            UpdateForwardingActive(false);
            if (launchOptions.LaunchGame && launchedProcess is not null && launchedProcessExited)
            {
                AddLog("Launched process exited; closing bridge.");
                RequestExit(0);
            }

            OnPropertyChanged(nameof(SteamInputStatusText));
            return;
        }

        bool receiverRunning = WindowsRuntime.IsAnyProcessRunning(receiverProcesses);
        if (receiverRunning)
        {
            hasSeenReceiverProcess = true;
        }

        if (launchOptions.LaunchGame && hasSeenReceiverProcess && !receiverRunning)
        {
            AddLog("Receiver process exited; closing bridge.");
            RequestExit(0);
            return;
        }

        bool receiverForeground = IsReceiverProcess(foregroundProcess);
        ReceiverStatus = receiverRunning ? "Receiver running" : "Receiver not running";
        ForwardingStatus = receiverForeground ? "Forwarding to foreground receiver" : "Preview only; waiting for receiver foreground";
        UpdateForwardingActive(receiverRunning && receiverForeground);
        OnPropertyChanged(nameof(SteamInputStatusText));

        if (Steam.SteamInputReader.TryReadLatest(out HidInputReport report))
        {
            _ = HandleOutputAsync(report, "Steam Input");
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
