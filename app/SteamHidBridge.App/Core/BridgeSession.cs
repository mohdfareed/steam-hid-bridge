using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SteamHidBridge.App.Configuration;
using SteamHidBridge.App.Platform;
using SteamHidBridge.App.Platform.App;

namespace SteamHidBridge.App.Core;

internal sealed record BridgeSessionStatus(
    bool ForwardingEnabled,
    BridgeOutputMode OutputMode,
    BoardOutputStatus BoardOutput);

internal sealed class BridgeSession : IDisposable
{
    private static readonly TimeSpan StatusInterval = TimeSpan.FromMilliseconds(250);
    private readonly BoardSerialMouseOutput boardOutput;
    private readonly SessionProcessMonitor processMonitor;
    private readonly SteamInputConfigForcer steamInputConfigForcer = new();
    private readonly CancellationTokenSource cancellation = new();
    private readonly Task statusTask;
    private bool isDisposed;
    private bool hasRequestedExit;
    private bool isForwarding;
    private BridgeOutputMode outputMode = BridgeOutputMode.Board;

    public BridgeSession(BridgeLaunchOptions launchOptions, int? boardPort)
    {
        processMonitor = new SessionProcessMonitor(!string.IsNullOrWhiteSpace(launchOptions.ProfileId));
        boardOutput = new BoardSerialMouseOutput(boardPort);
        statusTask = Task.Run(RunStatusLoopAsync);
    }

    public event Action<MouseInputFrame>? MouseInput;
    public event Action<BridgeSessionStatus>? StatusChanged;
    public event Action<int>? ExitRequested;

    public void PublishMouseInput(MouseInputFrame frame)
    {
        if (isForwarding && outputMode == BridgeOutputMode.Board)
        {
            boardOutput.Consume(frame);
        }

        MouseInput?.Invoke(frame);
    }

    public void SetOutputMode(BridgeOutputMode value)
    {
        outputMode = value;
        PublishStatus();
    }

    public void SetBoardPort(int? value)
    {
        boardOutput.SetPort(value);
        PublishStatus();
    }

    public void SetProfile(GameProfile value)
    {
        processMonitor.SetProfile(value);
        SetForwarding(false);
        PublishStatus();
    }

    public void LaunchProfile()
    {
        processMonitor.LaunchProfile();
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

        processMonitor.Dispose();
        steamInputConfigForcer.Reset();
        boardOutput.Dispose();
        cancellation.Dispose();
    }

    private async Task RunStatusLoopAsync()
    {
        using PeriodicTimer timer = new(StatusInterval);
        while (await timer.WaitForNextTickAsync(cancellation.Token).ConfigureAwait(false))
        {
            Refresh();
        }
    }

    private void Refresh()
    {
        if (isDisposed)
        {
            return;
        }

        if (outputMode == BridgeOutputMode.Board)
        {
            boardOutput.Refresh();
        }

        ReceiverState receiverState = processMonitor.Refresh();
        if (receiverState.ShouldExit)
        {
            SetForwarding(false);
            RequestExit();
            return;
        }

        SetForwarding(receiverState.ShouldForward);
        PublishStatus();
    }

    private void PublishStatus()
    {
        StatusChanged?.Invoke(new BridgeSessionStatus(isForwarding, outputMode, boardOutput.GetStatus()));
    }

    private void SetForwarding(bool value)
    {
        isForwarding = value;
        _ = steamInputConfigForcer.TrySetForced(value);
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
}
