using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SteamHidBridge.App.Configuration;
using SteamHidBridge.App.Platform.App;
using SteamHidBridge.App.Platform.Steam;
using SteamHidBridge.App.Platform.Windows;

namespace SteamHidBridge.App.Core;

internal sealed record BridgeRuntimeStatus(
    bool ForwardingEnabled,
    BridgeInputMode InputMode,
    InputSourceStatus InputStatus,
    BridgeOutputMode OutputMode,
    BoardOutputStatus BoardOutput);

internal sealed class BridgeRuntime : IDisposable
{
    private static readonly TimeSpan StatusInterval = TimeSpan.FromMilliseconds(250);
    private readonly BoardSerialMouseOutput boardOutput;
    private readonly MouseInputRouter mouseInputRouter;
    private readonly GameProcessHost gameProcessHost = new();
    private readonly SteamInputConfigForcer steamInputConfigForcer = new();
    private readonly CancellationTokenSource cancellation = new();
    private readonly Task statusTask;
    private readonly Lock syncLock = new();
    private readonly bool hasProfileLaunch;
    private GameProfile profile = new();
    private bool isDisposed;
    private bool hasRequestedExit;
    private bool hasSeenReceiverProcess;
    private bool isForwarding;
    private BridgeInputMode inputMode = BridgeInputMode.LegacyMouse;
    private BridgeOutputMode outputMode = BridgeOutputMode.Board;
    private InputSourceStatus inputStatus = new(InputSourceState.Inactive);

    public BridgeRuntime(BridgeLaunchOptions launchOptions, int? boardPort)
    {
        hasProfileLaunch = !string.IsNullOrWhiteSpace(launchOptions.ProfileId);
        boardOutput = new BoardSerialMouseOutput(boardPort);
        mouseInputRouter = new MouseInputRouter(OnMouseInput, ForwardMouseInput, () => isForwarding);
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
        PublishStatus();
    }

    public void SetInputStatus(InputSourceStatus value)
    {
        inputStatus = value;
        PublishStatus();
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
        lock (syncLock)
        {
            profile = value;
            hasSeenReceiverProcess = false;
        }

        SetForwarding(false);
        PublishStatus();
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

        string[] receivers;
        lock (syncLock)
        {
            receivers = [.. profile.ReceiverProcesses];
        }

        if (receivers.Length == 0)
        {
            SetForwarding(false);
            if (hasProfileLaunch && gameProcessHost.HasLaunchedProcess && gameProcessHost.HasExited)
            {
                RequestExit();
                return;
            }

            PublishStatus();
            return;
        }

        bool receiverRunning = WindowsRuntime.IsAnyProcessRunning(receivers);
        if (receiverRunning)
        {
            hasSeenReceiverProcess = true;
        }

        if (hasProfileLaunch && hasSeenReceiverProcess && !receiverRunning)
        {
            SetForwarding(false);
            RequestExit();
            return;
        }

        bool shouldForward = receiverRunning && IsReceiverProcess(WindowsRuntime.GetForegroundProcessName(), receivers);
        SetForwarding(shouldForward);
        PublishStatus();
    }

    private void PublishStatus()
    {
        StatusChanged?.Invoke(new BridgeRuntimeStatus(
            isForwarding,
            inputMode,
            inputStatus,
            outputMode,
            boardOutput.Status));
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

    private void StopReceiverProcesses()
    {
        string[] receivers;
        lock (syncLock)
        {
            receivers = [.. profile.ReceiverProcesses];
        }

        if (receivers.Length > 0)
        {
            _ = WindowsRuntime.StopProcessesByName(receivers);
        }
    }

    private void OnMouseInput(MouseInputFrame frame)
    {
        MouseInput?.Invoke(frame);
    }

    private void ForwardMouseInput(MouseInputFrame frame)
    {
        if (outputMode == BridgeOutputMode.Board)
        {
            boardOutput.Consume(frame);
        }
    }

    private static bool IsReceiverProcess(string processName, string[] receivers)
    {
        if (string.IsNullOrWhiteSpace(processName))
        {
            return false;
        }

        foreach (string receiver in receivers)
        {
            if (string.Equals(receiver, processName, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
