using System;
using System.IO;
using System.Threading;
using SteamHidBridge.App.Configuration;
using SteamHidBridge.App.Platform.Windows;

namespace SteamHidBridge.App.Core;

internal readonly record struct ReceiverState(bool ShouldForward, bool ShouldExit);

internal sealed class SessionProcessMonitor(bool exitWithOwnedLaunch, bool bypassReceiverGate) : IDisposable
{
    private readonly Lock syncLock = new();
    private readonly GameProcessHost gameProcessHost = new();
    private readonly bool exitWithOwnedLaunch = exitWithOwnedLaunch;
    private GameProfile profile = new();
    private bool hasOwnedLaunch;
    private bool hasSeenReceiverProcess;

    public bool HasRunningLaunch
    {
        get
        {
            lock (syncLock)
            {
                return hasOwnedLaunch && (!gameProcessHost.HasExited || hasSeenReceiverProcess);
            }
        }
    }

    public void SetProfile(GameProfile value)
    {
        lock (syncLock)
        {
            profile = PrepareProfile(value);
            hasSeenReceiverProcess = false;
        }
    }

    public void LaunchProfile()
    {
        GameProfile snapshot;
        lock (syncLock)
        {
            snapshot = profile.Copy();
            hasOwnedLaunch = true;
        }

        gameProcessHost.Launch(snapshot);
    }

    public ReceiverState Refresh()
    {
        if (bypassReceiverGate)
        {
            return new ReceiverState(
                true,
                exitWithOwnedLaunch && HasRunningLaunch && gameProcessHost.HasExited);
        }

        string[] receivers = GetReceivers();
        if (receivers.Length == 0)
        {
            return new ReceiverState(
                false,
                exitWithOwnedLaunch && HasRunningLaunch && gameProcessHost.HasExited);
        }

        bool receiverRunning = WindowsRuntime.IsAnyProcessRunning(receivers);
        lock (syncLock)
        {
            if (receiverRunning)
            {
                hasSeenReceiverProcess = true;
            }

            if (exitWithOwnedLaunch && hasOwnedLaunch && hasSeenReceiverProcess && !receiverRunning)
            {
                return new ReceiverState(false, true);
            }
        }

        return new ReceiverState(
            receiverRunning && IsForegroundReceiver(receivers),
            false);
    }

    public void StopReceivers()
    {
        string[] receivers = GetReceivers();
        if (receivers.Length > 0)
        {
            _ = WindowsRuntime.StopProcessesByName(receivers);
        }
    }

    public void Dispose()
    {
        gameProcessHost.Dispose();
        StopReceivers();
    }

    private string[] GetReceivers()
    {
        lock (syncLock)
        {
            return [.. profile.ReceiverProcesses];
        }
    }

    private static bool IsForegroundReceiver(string[] receivers)
    {
        string processName = WindowsRuntime.GetForegroundProcessName();
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

    private static GameProfile PrepareProfile(GameProfile value)
    {
        GameProfile profile = value.Copy();
        if (profile.ReceiverProcesses.Count > 0)
        {
            return profile;
        }

        string executableName = Path.GetFileName(profile.Executable.Trim());
        if (!string.IsNullOrWhiteSpace(executableName))
        {
            profile.ReceiverProcesses.Add(executableName);
        }

        return profile;
    }
}
