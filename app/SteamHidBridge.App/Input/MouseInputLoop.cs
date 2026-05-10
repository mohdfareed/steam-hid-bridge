using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SteamHidBridge.App.Steam;

namespace SteamHidBridge.App.Input;

public sealed class MouseInputLoop : IDisposable
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(4);
    private readonly SteamMouseInputEmitter emitter;
    private readonly Action<MouseInputFrame> previewFrame;
    private readonly IEnumerable<IMouseInputConsumer> forwardingConsumers;
    private readonly Func<bool> isForwardingEnabled;
    private readonly CancellationTokenSource cancellation = new();
    private readonly Task loopTask;
    private MouseInputLoopStatistics statistics = new(PollInterval, 0, 0, 0, 0);
    private bool isDisposed;

    public MouseInputLoop(
        SteamMouseInputEmitter emitter,
        Action<MouseInputFrame> previewFrame,
        IEnumerable<IMouseInputConsumer> forwardingConsumers,
        Func<bool> isForwardingEnabled)
    {
        this.emitter = emitter;
        this.previewFrame = previewFrame;
        this.forwardingConsumers = forwardingConsumers;
        this.isForwardingEnabled = isForwardingEnabled;
        loopTask = Task.Run(RunAsync);
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
            loopTask.Wait(TimeSpan.FromSeconds(1));
        }
        catch (AggregateException ex) when (ex.InnerException is OperationCanceledException)
        {
        }

        emitter.Shutdown();
        cancellation.Dispose();
    }

    public MouseInputLoopStatistics GetStatistics()
    {
        return statistics;
    }

    private async Task RunAsync()
    {
        using PeriodicTimer timer = new(PollInterval);
        long pollCount = 0;
        long frameCount = 0;
        long lastTimestamp = Environment.TickCount64;
        long lastPollCount = 0;
        long lastFrameCount = 0;

        while (await timer.WaitForNextTickAsync(cancellation.Token).ConfigureAwait(false))
        {
            pollCount++;

            if (emitter.TryReadLatest(out MouseInputFrame frame))
            {
                frameCount++;
                previewFrame(frame);
                if (isForwardingEnabled())
                {
                    foreach (IMouseInputConsumer consumer in forwardingConsumers)
                    {
                        consumer.Consume(frame);
                    }
                }
            }

            long now = Environment.TickCount64;
            long elapsed = now - lastTimestamp;
            if (elapsed < 1000)
            {
                continue;
            }

            statistics = new MouseInputLoopStatistics(
                PollInterval,
                pollCount,
                frameCount,
                (pollCount - lastPollCount) * 1000d / elapsed,
                (frameCount - lastFrameCount) * 1000d / elapsed);

            lastTimestamp = now;
            lastPollCount = pollCount;
            lastFrameCount = frameCount;
        }
    }
}
