using System;
using System.Threading;
using System.Threading.Tasks;
using SteamHidBridge.App.Steam;
using SteamHidBridge.Protocol;

namespace SteamHidBridge.App.Input;

public readonly record struct MouseInputFrame(
    short PointerDeltaX,
    short PointerDeltaY,
    sbyte VerticalWheel,
    MouseButtons Buttons);

public sealed record MouseInputLoopStatistics(
    TimeSpan PollInterval,
    long PollCount,
    long FrameCount,
    double PollsPerSecond,
    double FramesPerSecond);

public sealed class MouseInputLoop : IDisposable
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(4);
    private readonly SteamMouseInputEmitter emitter;
    private readonly MouseInputPipeline pipeline;
    private readonly Func<bool> isForwardingEnabled;
    private readonly CancellationTokenSource cancellation = new();
    private readonly Task loopTask;
    private MouseInputLoopStatistics statistics = new(PollInterval, 0, 0, 0, 0);
    private bool isDisposed;

    public MouseInputLoop(
        SteamMouseInputEmitter emitter,
        MouseInputPipeline pipeline,
        Func<bool> isForwardingEnabled)
    {
        this.emitter = emitter;
        this.pipeline = pipeline;
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
                pipeline.Publish(frame, isForwardingEnabled());
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
