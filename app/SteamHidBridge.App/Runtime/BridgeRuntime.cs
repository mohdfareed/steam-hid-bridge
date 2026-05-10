using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SteamHidBridge.App.Infrastructure;
using SteamHidBridge.App.Input;
using SteamHidBridge.App.Profiles;
using SteamHidBridge.App.Startup;
using SteamHidBridge.App.Steam;

namespace SteamHidBridge.App.Runtime;

public sealed class BridgeRuntime : IDisposable
{
    private static readonly TimeSpan StatusInterval = TimeSpan.FromMilliseconds(250);
    private readonly BridgeLaunchOptions launchOptions;
    private readonly SteamMouseInputEmitter steamMouseInputEmitter = new();
    private readonly MouseInputLoop mouseInputLoop;
    private readonly GameProcessHost gameProcessHost;
    private readonly ForwardingGate forwardingGate;
    private readonly CancellationTokenSource cancellation = new();
    private readonly Task statusTask;
    private readonly Lock syncLock = new();
    private string profileId = "";
    private GameProfile profile = new();
    private bool isDisposed;

    public BridgeRuntime(BridgeLaunchOptions launchOptions, IEnumerable<IMouseInputConsumer> forwardingConsumers)
    {
        this.launchOptions = launchOptions;
        gameProcessHost = new GameProcessHost(SetActivity);
        forwardingGate = new ForwardingGate(launchOptions.SteamAppId, SetActivity);
        mouseInputLoop = new MouseInputLoop(steamMouseInputEmitter, OnMouseInput, forwardingConsumers, () => forwardingGate.IsForwarding);
        statusTask = Task.Run(RunStatusLoopAsync);
    }

    public event Action<MouseInputFrame>? MouseInput;
    public event Action<BridgeRuntimeStatus>? StatusChanged;
    public event Action<string, bool>? ActivityChanged;
    public event Action<int>? ExitRequested;

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
        catch (AggregateException ex) when (ex.InnerException is OperationCanceledException)
        {
        }

        gameProcessHost.Dispose();
        forwardingGate.Dispose();
        mouseInputLoop.Dispose();
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
        MouseInputLoopStatistics statistics = mouseInputLoop.GetStatistics();
        string inputLoopText = statistics.PollCount == 0
            ? "input loop starting"
            : $"poll {statistics.PollsPerSecond:F0}/s, frames {statistics.FramesPerSecond:F0}/s";

        StatusChanged?.Invoke(new BridgeRuntimeStatus(forwardingGate.IsForwarding, forwardingText, inputLoopText));
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
        SetActivity(reason, isError: true);
        ExitRequested?.Invoke(0);
    }

    private void OnMouseInput(MouseInputFrame frame)
    {
        MouseInput?.Invoke(frame);
    }
}
