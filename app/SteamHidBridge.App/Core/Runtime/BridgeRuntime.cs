using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SteamHidBridge.App.Configuration;
using SteamHidBridge.App.Core.Input;
using SteamHidBridge.App.Platform.App;
using SteamHidBridge.App.Platform.Windows;

namespace SteamHidBridge.App.Core.Runtime;

internal sealed record BridgeRuntimeStatus(
    bool ForwardingEnabled,
    MouseInputStatistics Statistics,
    BridgeInputMode InputMode,
    InputSourceStatus InputStatus,
    OutputStatus[] Outputs);

internal sealed class BridgeRuntime : IDisposable
{
    private static readonly TimeSpan StatusInterval = TimeSpan.FromMilliseconds(250);
    private readonly IOutputStatusProvider[] outputStatusProviders;
    private readonly MouseInputRouter mouseInputRouter;
    private readonly GameProcessHost gameProcessHost;
    private readonly ForwardingGate forwardingGate;
    private readonly CancellationTokenSource cancellation = new();
    private readonly Task statusTask;
    private readonly Lock syncLock = new();
    private readonly bool hasProfileLaunch;
    private GameProfile profile = new();
    private bool isDisposed;
    private bool hasRequestedExit;
    private BridgeInputMode inputMode = BridgeInputMode.LegacyMouse;
    private InputSourceStatus inputStatus = new(InputSourceState.Inactive);

    public BridgeRuntime(BridgeLaunchOptions launchOptions, IEnumerable<IMouseInputConsumer> forwardingConsumers)
    {
        hasProfileLaunch = !string.IsNullOrWhiteSpace(launchOptions.ProfileId);
        IMouseInputConsumer[] consumers = [.. forwardingConsumers];
        outputStatusProviders = [.. consumers.OfType<IOutputStatusProvider>()];
        gameProcessHost = new GameProcessHost();
        forwardingGate = new ForwardingGate();
        mouseInputRouter = new MouseInputRouter(OnMouseInput, consumers, () => forwardingGate.IsForwarding);
        statusTask = Task.Run(RunStatusLoopAsync);
    }

    public event Action<MouseInputFrame>? MouseInput;
    public event Action<BridgeRuntimeStatus>? StatusChanged;
    public event Action<int>? ExitRequested;

    public void PublishLegacyMouseInput(MouseInputFrame frame)
    {
        if (inputMode == BridgeInputMode.LegacyMouse)
        {
            mouseInputRouter.Publish(frame);
        }
    }

    public void PublishSteamInputMouseInput(MouseInputFrame frame)
    {
        if (inputMode == BridgeInputMode.SteamInputActions)
        {
            mouseInputRouter.Publish(frame);
        }
    }

    public void SetInputMode(BridgeInputMode value)
    {
        inputMode = value;
        inputStatus = value == BridgeInputMode.SteamInputActions
            ? new InputSourceStatus(InputSourceState.Starting)
            : new InputSourceStatus(InputSourceState.Inactive);
        RefreshStatus();
    }

    public void SetInputStatus(InputSourceStatus value)
    {
        inputStatus = value;
        RefreshStatus();
    }

    public void SetProfile(GameProfile value)
    {
        lock (syncLock)
        {
            profile = CloneProfile(value);
            forwardingGate.ResetProfile();
        }

        RefreshStatus();
    }

    public void LaunchProfile()
    {
        GameProfile snapshot;
        lock (syncLock)
        {
            snapshot = profile;
        }

        gameProcessHost.Launch(snapshot);
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
            _ = statusTask.Wait(TimeSpan.FromSeconds(1));
        }
        catch (AggregateException ex) when (ex.InnerExceptions.All(static exception => exception is OperationCanceledException))
        {
        }
        catch (AggregateException)
        {
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

        foreach (IOutputStatusProvider provider in outputStatusProviders)
        {
            provider.Refresh();
        }

        string[] receivers;
        lock (syncLock)
        {
            receivers = [.. profile.ReceiverProcesses];
        }

        if (receivers.Length == 0)
        {
            PublishStatus(false);
            if (hasProfileLaunch && gameProcessHost.HasLaunchedProcess && gameProcessHost.HasExited)
            {
                RequestExit();
            }

            return;
        }

        ForwardingGateResult gateResult = forwardingGate.Refresh(receivers, hasProfileLaunch);
        if (gateResult.ReceiverExited)
        {
            RequestExit();
            return;
        }

        PublishStatus(gateResult.IsForwarding);

    }

    private void PublishStatus(bool forwardingEnabled)
    {
        MouseInputStatistics statistics = mouseInputRouter.GetStatistics();

        StatusChanged?.Invoke(new BridgeRuntimeStatus(
            forwardingEnabled,
            statistics,
            inputMode,
            inputStatus,
            [.. outputStatusProviders.Select(static provider => provider.Status)]));
    }

    private void RequestExit()
    {
        if (hasRequestedExit)
        {
            return;
        }

        hasRequestedExit = true;
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

        _ = WindowsRuntime.StopProcessesByName(receivers);
    }

    private void OnMouseInput(MouseInputFrame frame)
    {
        MouseInput?.Invoke(frame);
    }

    private static GameProfile CloneProfile(GameProfile profile)
    {
        return new GameProfile
        {
            Title = profile.Title,
            Executable = profile.Executable,
            Arguments = profile.Arguments,
            WorkingDirectory = profile.WorkingDirectory,
            InputMode = profile.InputMode,
            OutputMode = profile.OutputMode,
            ReceiverProcesses = [.. profile.ReceiverProcesses]
        };
    }

}
