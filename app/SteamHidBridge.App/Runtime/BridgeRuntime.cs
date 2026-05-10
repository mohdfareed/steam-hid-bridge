using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SteamHidBridge.App.Infrastructure;
using SteamHidBridge.App.Input;
using SteamHidBridge.App.Profiles;
using SteamHidBridge.App.Startup;
using SteamHidBridge.App.Windows;

namespace SteamHidBridge.App.Runtime;

public sealed class BridgeRuntime : IDisposable
{
    private static readonly TimeSpan StatusInterval = TimeSpan.FromMilliseconds(250);
    private readonly BridgeLaunchOptions launchOptions;
    private readonly MouseInputRouter mouseInputRouter;
    private readonly GameProcessHost gameProcessHost;
    private readonly ForwardingGate forwardingGate;
    private readonly CancellationTokenSource cancellation = new();
    private readonly Task statusTask;
    private readonly Lock syncLock = new();
    private string profileId = "";
    private GameProfile profile = new();
    private bool isDisposed;
    private bool hasRequestedExit;

    public BridgeRuntime(BridgeLaunchOptions launchOptions, IEnumerable<IMouseInputConsumer> forwardingConsumers)
    {
        this.launchOptions = launchOptions;
        gameProcessHost = new GameProcessHost(SetActivity);
        forwardingGate = new ForwardingGate(SetActivity);
        mouseInputRouter = new MouseInputRouter(OnMouseInput, forwardingConsumers, () => forwardingGate.IsForwarding);
        statusTask = Task.Run(RunStatusLoopAsync);
    }

    public event Action<MouseInputFrame>? MouseInput;
    public event Action<BridgeRuntimeStatus>? StatusChanged;
    public event Action<string, bool>? ActivityChanged;
    public event Action<int>? ExitRequested;

    public void PublishMouseInput(MouseInputFrame frame)
    {
        mouseInputRouter.Publish(frame);
    }

    public void SetProfile(string id, GameProfile value)
    {
        lock (syncLock)
        {
            profileId = id;
            profile = value;
            forwardingGate.ResetProfile();
        }

        RefreshStatus();
    }

    public void LaunchProfile()
    {
        GameProfile snapshot;
        string id;
        lock (syncLock)
        {
            snapshot = profile;
            id = profileId;
        }

        gameProcessHost.Launch(id, snapshot);
    }

    public void Dispose()
    {
        if (isDisposed)
        {
            return;
        }

        isDisposed = true;
        cancellation.Cancel();
        try
        {
            statusTask.Wait(TimeSpan.FromSeconds(1));
        }
        catch (AggregateException ex) when (ex.InnerExceptions.All(static exception => exception is OperationCanceledException))
        {
        }
        catch (AggregateException ex)
        {
            AppLog.WriteException("status-loop-dispose-failed", ex);
        }

        gameProcessHost.Dispose();
        StopReceiverProcesses();
        forwardingGate.Dispose();
        cancellation.Dispose();
    }

    private async Task RunStatusLoopAsync()
    {
        using PeriodicTimer timer = new(StatusInterval);
        while (await timer.WaitForNextTickAsync(cancellation.Token).ConfigureAwait(false))
        {
            RefreshStatus();
        }
    }

    private void RefreshStatus()
    {
        if (isDisposed)
        {
            return;
        }

        string[] receivers;
        lock (syncLock)
        {
            receivers = [.. profile.ReceiverProcesses];
        }

        if (receivers.Length == 0)
        {
            PublishStatus("Forwarding off");
            if (launchOptions.LaunchGame && gameProcessHost.HasLaunchedProcess && gameProcessHost.HasExited)
            {
                RequestExit("Launched process exited; closing bridge.");
            }

            return;
        }

        ForwardingGateResult gateResult = forwardingGate.Refresh(receivers, launchOptions.LaunchGame);
        if (gateResult.ReceiverExited)
        {
            RequestExit("Receiver process exited; closing bridge.");
            return;
        }

        PublishStatus(gateResult.StatusText);

    }

    private void PublishStatus(string forwardingText)
    {
        MouseInputStatistics statistics = mouseInputRouter.GetStatistics();
        string inputLoopText = statistics.EventCount == 0
            ? "waiting for mouse input"
            : $"events {statistics.EventsPerSecond:F0}/s, preview {statistics.PreviewFramesPerSecond:F0}/s";

        StatusChanged?.Invoke(new BridgeRuntimeStatus(
            forwardingGate.IsForwarding,
            forwardingText,
            inputLoopText,
            "Legacy mouse observer active."));
    }

    private void SetActivity(string value, bool isError = false)
    {
        if (isDisposed)
        {
            AppLog.Write(value);
            return;
        }

        AppLog.Write(value);
        ActivityChanged?.Invoke(value, isError);
        PublishStatus(forwardingGate.IsForwarding ? "Forwarding on" : "Forwarding off");
    }

    private void RequestExit(string reason)
    {
        if (hasRequestedExit)
        {
            return;
        }

        hasRequestedExit = true;
        SetActivity(reason, isError: true);
        ExitRequested?.Invoke(0);
    }

    private void StopReceiverProcesses()
    {
        string[] receivers;
        lock (syncLock)
        {
            receivers = [.. profile.ReceiverProcesses];
        }

        if (receivers.Length == 0)
        {
            return;
        }

        int stoppedCount = WindowsRuntime.StopProcessesByName(receivers);
        if (stoppedCount > 0)
        {
            AppLog.Write($"stopped receiver processes count={stoppedCount}");
        }
    }

    private void OnMouseInput(MouseInputFrame frame)
    {
        MouseInput?.Invoke(frame);
    }
}
