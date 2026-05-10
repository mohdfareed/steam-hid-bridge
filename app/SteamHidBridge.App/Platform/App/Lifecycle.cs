using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using SteamHidBridge.App.Configuration;

namespace SteamHidBridge.App.Platform.App;

public static class StartupSync
{
    public static void Run(AppSettingsStore settingsStore)
    {
        try
        {
            settingsStore.SaveCurrent();
            AppLog.Write($"settings synced path={settingsStore.FilePath}");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or InvalidOperationException)
        {
            AppLog.WriteException("settings-sync-failed", ex);
        }

        try
        {
            SrmManifestWriteResult result = new SrmManifestWriter(settingsStore).Write(
                settingsStore.Document.General.SrmManifestPath,
                Environment.ProcessPath);
            AppLog.Write($"srm manifest synced path={result.Path} profiles={result.ProfileCount}");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or InvalidOperationException)
        {
            AppLog.WriteException("srm-manifest-sync-failed", ex);
        }
    }
}

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
