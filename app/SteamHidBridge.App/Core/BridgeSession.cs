using System;
using System.Linq;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using SteamHidBridge.App.Configuration;
using SteamHidBridge.App.Platform;
using SteamHidBridge.App.Platform.Teensy;
using SteamHidBridge.App.Platform.Viiper;
using SteamHidBridge.Protocol;

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
    private readonly Lock syncLock = new();
    private readonly TeensySerialMouseOutput boardOutput;
    private readonly ViiperMouseOutput viiperOutput;
    private readonly SessionProcessMonitor processMonitor;
    private readonly SteamInputConfigForcer steamInputConfigForcer = new();
    private readonly Channel<QueuedOutput> outputQueue = Channel.CreateUnbounded<QueuedOutput>(new UnboundedChannelOptions
    {
        SingleReader = true,
        SingleWriter = false
    });
    private readonly CancellationTokenSource cancellation = new();
    private readonly Task outputTask;
    private readonly Task statusTask;
    private bool isDisposed;
    private bool hasRequestedExit;
    private bool isForwarding;
    private long outputGeneration;
    private BridgeOutputMode outputMode = BridgeOutputMode.Board;
    private MouseButtons manualBoardButtons;

    public BridgeSession(int? boardPort, string viiperHost, int viiperPort, bool exitWithOwnedLaunch, bool bypassReceiverGate)
    {
        processMonitor = new SessionProcessMonitor(exitWithOwnedLaunch, bypassReceiverGate);
        boardOutput = new TeensySerialMouseOutput(boardPort);
        viiperOutput = new ViiperMouseOutput(viiperHost, viiperPort);
        outputTask = Task.Run(RunOutputLoopAsync);
        statusTask = Task.Run(RunStatusLoopAsync);
    }

    public event Action<BridgeSessionStatus>? StatusChanged;
    public event Action<int>? ExitRequested;

    public void PublishMouseInput(MouseInputFrame frame)
    {
        BridgeOutputMode queuedMode;
        long queuedGeneration;

        lock (syncLock)
        {
            if (!isForwarding || outputMode == BridgeOutputMode.None)
            {
                return;
            }

            queuedMode = outputMode;
            queuedGeneration = outputGeneration;
        }

        _ = outputQueue.Writer.TryWrite(new QueuedOutput(queuedMode, queuedGeneration, frame));
    }

    public void SetOutputMode(BridgeOutputMode value)
    {
        BridgeOutputMode previousMode;
        lock (syncLock)
        {
            if (outputMode == value)
            {
                PublishStatus();
                return;
            }

            previousMode = outputMode;
            outputMode = value;
            outputGeneration++;
        }

        ResetOutputState(previousMode);
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

    public void SetBoardDiagnosticButtons(MouseButtons buttons)
    {
        lock (syncLock)
        {
            manualBoardButtons = buttons;
        }

        boardOutput.WriteFrame(new MouseInputFrame(0, 0, 0, buttons));
    }

    public void SendBoardDiagnosticFrame(short deltaX, short deltaY, sbyte wheel)
    {
        MouseButtons buttons;
        lock (syncLock)
        {
            buttons = manualBoardButtons;
        }

        boardOutput.WriteFrame(new MouseInputFrame(deltaX, deltaY, wheel, buttons));
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
            _ = outputTask.Wait(TimeSpan.FromSeconds(1));
        }
        catch (AggregateException ex) when (ex.InnerExceptions.All(static exception => exception is OperationCanceledException))
        {
        }
        catch (AggregateException)
        {
        }

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

    private async Task RunOutputLoopAsync()
    {
        try
        {
            while (await outputQueue.Reader.WaitToReadAsync(cancellation.Token).ConfigureAwait(false))
            {
                while (outputQueue.Reader.TryRead(out QueuedOutput queued))
                {
                    IMouseOutputTarget? target = GetQueuedTarget(queued);
                    target?.WriteFrame(queued.Frame);
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
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

        boardOutput.Refresh();
        viiperOutput.Refresh();

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
        bool forwardingEnabled;
        BridgeOutputMode currentMode;
        lock (syncLock)
        {
            forwardingEnabled = isForwarding;
            currentMode = outputMode;
        }

        StatusChanged?.Invoke(new BridgeSessionStatus(
            forwardingEnabled,
            processMonitor.HasRunningLaunch,
            currentMode,
            boardOutput.GetStatus(),
            viiperOutput.GetStatus()));
    }

    private void SetForwarding(bool value)
    {
        BridgeOutputMode modeToReset;
        bool reset;
        lock (syncLock)
        {
            reset = isForwarding && !value;
            modeToReset = outputMode;
            isForwarding = value;
            if (reset)
            {
                outputGeneration++;
            }
        }

        if (reset)
        {
            ResetOutputState(modeToReset);
        }

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

    private IMouseOutputTarget? GetQueuedTarget(QueuedOutput queued)
    {
        lock (syncLock)
        {
            return isDisposed || queued.Generation != outputGeneration || queued.Mode != outputMode
                ? null
                : queued.Mode switch
                {
                    BridgeOutputMode.Board => boardOutput,
                    BridgeOutputMode.Viiper => viiperOutput,
                    BridgeOutputMode.None => null,
                    _ => null
                };
        }
    }

    private readonly record struct QueuedOutput(BridgeOutputMode Mode, long Generation, MouseInputFrame Frame);
}
