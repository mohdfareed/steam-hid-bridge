using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SteamHidBridge.App.Configuration;
using SteamHidBridge.App.Platform;
using SteamHidBridge.App.Platform.Teensy;
using SteamHidBridge.App.Platform.Viiper;

namespace SteamHidBridge.App.Core;

internal sealed record BridgeSessionStatus(
    bool ForwardingEnabled,
    bool HasRunningLaunch,
    BridgeOutputMode OutputMode,
    OutputStatus BoardOutput,
    OutputStatus ViiperOutput);

internal sealed class BridgeSession : IDisposable
{
    private static readonly TimeSpan StatusInterval = TimeSpan.FromMilliseconds(250);
    private readonly TeensySerialMouseOutput boardOutput;
    private readonly ViiperMouseOutput viiperOutput;
    private readonly SessionProcessMonitor processMonitor;
    private readonly SteamInputConfigForcer steamInputConfigForcer = new();
    private readonly CancellationTokenSource cancellation = new();
    private readonly Task statusTask;
    private bool isDisposed;
    private bool hasRequestedExit;
    private bool isForwarding;
    private BridgeOutputMode outputMode = BridgeOutputMode.Board;

    public BridgeSession(int? boardPort, string viiperHost, int viiperPort)
    {
        processMonitor = new SessionProcessMonitor();
        boardOutput = new TeensySerialMouseOutput(boardPort);
        viiperOutput = new ViiperMouseOutput(viiperHost, viiperPort);
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
        else if (isForwarding && outputMode == BridgeOutputMode.Viiper)
        {
            viiperOutput.Consume(frame);
        }

        MouseInput?.Invoke(frame);
    }

    public void SetOutputMode(BridgeOutputMode value)
    {
        if (outputMode == value)
        {
            PublishStatus();
            return;
        }

        ResetOutputState(outputMode);
        outputMode = value;
        viiperOutput.SetEnabled(value == BridgeOutputMode.Viiper);
        PublishStatus();
    }

    public void SetBoardPort(int? value)
    {
        boardOutput.SetPort(value);
        PublishStatus();
    }

    public void SetViiperEndpoint(string host, int port)
    {
        viiperOutput.SetEndpoint(host, port);
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
        viiperOutput.Dispose();
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
        else if (outputMode == BridgeOutputMode.Viiper)
        {
            viiperOutput.Refresh();
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
        StatusChanged?.Invoke(new BridgeSessionStatus(
            isForwarding,
            processMonitor.HasRunningLaunch,
            outputMode,
            boardOutput.GetStatus(),
            viiperOutput.GetStatus()));
    }

    private void SetForwarding(bool value)
    {
        if (isForwarding && !value)
        {
            ResetOutputState(outputMode);
        }

        isForwarding = value;
        _ = steamInputConfigForcer.TrySetForced(value);
    }

    private void ResetOutputState(BridgeOutputMode mode)
    {
        if (mode == BridgeOutputMode.Board)
        {
            boardOutput.ResetState();
        }
        else if (mode == BridgeOutputMode.Viiper)
        {
            viiperOutput.ResetState();
        }
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
