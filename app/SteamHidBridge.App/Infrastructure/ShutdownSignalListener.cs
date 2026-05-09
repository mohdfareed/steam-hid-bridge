using System;
using System.Threading;
using System.Threading.Tasks;

namespace SteamHidBridge.App.Infrastructure;

public sealed class ShutdownSignalListener : IDisposable
{
    public const string SignalName = @"Local\SteamHidBridge.ShutdownForUpdate";

    private readonly EventWaitHandle shutdownEvent = new(false, EventResetMode.ManualReset, SignalName);
    private readonly CancellationTokenSource cancellation = new();
    private readonly Task waitTask;

    public ShutdownSignalListener(Action onShutdownRequested)
    {
        waitTask = Task.Run(() => WaitForSignal(onShutdownRequested));
    }

    public void Dispose()
    {
        cancellation.Cancel();

        try
        {
            waitTask.Wait(TimeSpan.FromSeconds(1));
        }
        catch (AggregateException)
        {
        }

        cancellation.Dispose();
        shutdownEvent.Dispose();
    }

    private void WaitForSignal(Action onShutdownRequested)
    {
        var signaled = WaitHandle.WaitAny([shutdownEvent, cancellation.Token.WaitHandle]);
        if (signaled == 0 && !cancellation.IsCancellationRequested)
        {
            onShutdownRequested();
        }
    }
}
