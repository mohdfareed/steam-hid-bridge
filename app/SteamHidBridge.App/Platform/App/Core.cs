using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;

namespace SteamHidBridge.App.Platform.App;

internal sealed record BridgeLaunchOptions(string ProfileId)
{
    public static BridgeLaunchOptions Parse(string[] args)
    {
        string profileId = string.Empty;

        for (int index = 0; index < args.Length; index++)
        {
            if (string.Equals(args[index], "--profile", StringComparison.OrdinalIgnoreCase) && index + 1 < args.Length)
            {
                profileId = args[index + 1].Trim();
                index++;
                continue;
            }

            if (TryReadValue(args[index], "--profile=", out string profileValue))
            {
                profileId = profileValue.Trim();
                continue;
            }
        }

        return new BridgeLaunchOptions(profileId);
    }

    private static bool TryReadValue(string arg, string prefix, out string value)
    {
        if (arg.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            value = arg[prefix.Length..];
            return true;
        }

        value = string.Empty;
        return false;
    }
}

internal sealed class AsyncRelayCommand(Func<Task> execute, Func<bool>? canExecute = null) : ICommand
{
    private bool isExecuting;

    public event EventHandler? CanExecuteChanged;

    public bool CanExecute(object? parameter)
    {
        return !isExecuting && (canExecute?.Invoke() ?? true);
    }

    public async void Execute(object? parameter)
    {
        if (!CanExecute(parameter))
        {
            return;
        }

        isExecuting = true;
        RaiseCanExecuteChanged();

        try
        {
            await execute();
        }
        catch (Exception ex)
        {
            Trace.TraceError($"command-failed{Environment.NewLine}{ex}");
        }
        finally
        {
            isExecuting = false;
            RaiseCanExecuteChanged();
        }
    }

    public void RaiseCanExecuteChanged()
    {
        CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }
}


internal sealed class ShutdownSignalListener : IDisposable
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
            _ = waitTask.Wait(TimeSpan.FromSeconds(1));
        }
        catch (AggregateException)
        {
        }

        cancellation.Dispose();
        shutdownEvent.Dispose();
    }

    private void WaitForSignal(Action onShutdownRequested)
    {
        int signaled = WaitHandle.WaitAny([shutdownEvent, cancellation.Token.WaitHandle]);
        if (signaled == 0 && !cancellation.IsCancellationRequested)
        {
            onShutdownRequested();
        }
    }
}
