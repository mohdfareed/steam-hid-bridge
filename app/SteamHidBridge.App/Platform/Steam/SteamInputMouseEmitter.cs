using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SteamHidBridge.App.Core.Input;
using SteamHidBridge.Protocol;
using Steamworks;

namespace SteamHidBridge.App.Platform.Steam;

internal sealed class SteamInputMouseEmitter(Action<MouseInputFrame> publishFrame, Action<InputSourceStatus> publishStatus) : IDisposable
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(1);
    private readonly CancellationTokenSource cancellation = new();
    private readonly Lock syncLock = new();
    private Task? pollTask;
    private bool enabled;
    private bool initialized;
    private bool disposed;
    private MouseButtons lastButtons;
    private long nextInitAttempt;
    private InputActionSetHandle_t actionSet;
    private InputAnalogActionHandle_t pointerAction;
    private InputDigitalActionHandle_t leftClick;
    private InputDigitalActionHandle_t rightClick;
    private InputDigitalActionHandle_t middleClick;
    private InputDigitalActionHandle_t backClick;
    private InputDigitalActionHandle_t forwardClick;
    private InputDigitalActionHandle_t wheelUp;
    private InputDigitalActionHandle_t wheelDown;
    private InputSourceStatus status = new(InputSourceState.Inactive);

    public void SetEnabled(bool value)
    {
        lock (syncLock)
        {
            enabled = value;
            status = value
                ? new InputSourceStatus(InputSourceState.Starting)
                : new InputSourceStatus(InputSourceState.Inactive);
        }

        publishStatus(status);

        if (value)
        {
            pollTask ??= Task.Run(PollAsync);
        }
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        cancellation.Cancel();
        try
        {
            _ = (pollTask?.Wait(TimeSpan.FromSeconds(1)));
        }
        catch (AggregateException ex) when (ex.InnerExceptions.All(static exception => exception is OperationCanceledException))
        {
        }
        catch (AggregateException ex)
        {
            Trace.TraceError($"steam-input-dispose-failed{Environment.NewLine}{ex}");
        }

        if (initialized)
        {
            try
            {
                _ = SteamInput.Shutdown();
                SteamAPI.Shutdown();
            }
            catch (Exception ex)
            {
                Trace.TraceError($"steam-input-shutdown-failed{Environment.NewLine}{ex}");
            }
        }

        cancellation.Dispose();
    }

    private async Task PollAsync()
    {
        while (!cancellation.IsCancellationRequested)
        {
            if (IsEnabled() && EnsureInitialized())
            {
                PollOnce();
            }

            await Task.Delay(PollInterval, cancellation.Token).ConfigureAwait(false);
        }
    }

    private bool IsEnabled()
    {
        lock (syncLock)
        {
            return enabled;
        }
    }

    private bool EnsureInitialized()
    {
        if (initialized)
        {
            return true;
        }

        long now = Environment.TickCount64;
        if (now < nextInitAttempt)
        {
            return false;
        }

        nextInitAttempt = now + 2000;
        try
        {
            string manifestPath = SteamInputActionManifest.Write();
            ESteamAPIInitResult initResult = SteamAPI.InitEx(out string error);
            if (initResult != ESteamAPIInitResult.k_ESteamAPIInitResult_OK)
            {
                InputSourceStatus next = new(InputSourceState.Error, Detail: $"{initResult}. {error}".Trim());
                if (next != status)
                {
                    status = next;
                    publishStatus(status);
                }

                return false;
            }

            if (!SteamInput.SetInputActionManifestFilePath(manifestPath))
            {
                InputSourceStatus next = new(InputSourceState.Error, Detail: $"Could not load action manifest {manifestPath}");
                if (next != status)
                {
                    status = next;
                    publishStatus(status);
                }

                return false;
            }

            if (!SteamInput.Init(false))
            {
                InputSourceStatus next = new(InputSourceState.Error, Detail: "SteamInput.Init failed.");
                if (next != status)
                {
                    status = next;
                    publishStatus(status);
                }

                return false;
            }

            actionSet = SteamInput.GetActionSetHandle(SteamInputActionManifest.ActionSet);
            pointerAction = SteamInput.GetAnalogActionHandle(SteamInputActionManifest.Pointer);
            leftClick = SteamInput.GetDigitalActionHandle(SteamInputActionManifest.LeftClick);
            rightClick = SteamInput.GetDigitalActionHandle(SteamInputActionManifest.RightClick);
            middleClick = SteamInput.GetDigitalActionHandle(SteamInputActionManifest.MiddleClick);
            backClick = SteamInput.GetDigitalActionHandle(SteamInputActionManifest.BackClick);
            forwardClick = SteamInput.GetDigitalActionHandle(SteamInputActionManifest.ForwardClick);
            wheelUp = SteamInput.GetDigitalActionHandle(SteamInputActionManifest.WheelUp);
            wheelDown = SteamInput.GetDigitalActionHandle(SteamInputActionManifest.WheelDown);
            initialized = true;
            InputSourceStatus ready = new(InputSourceState.Ready);
            if (ready != status)
            {
                status = ready;
                publishStatus(status);
            }

            return true;
        }
        catch (Exception ex) when (ex is DllNotFoundException or InvalidOperationException or EntryPointNotFoundException)
        {
            Trace.TraceError($"steam-input-init-failed{Environment.NewLine}{ex}");
            InputSourceStatus next = new(InputSourceState.Error, Detail: ex.Message);
            if (next != status)
            {
                status = next;
                publishStatus(status);
            }

            return false;
        }
    }

    private void PollOnce()
    {
        SteamAPI.RunCallbacks();
        SteamInput.RunFrame(false);

        InputHandle_t[] handles = new InputHandle_t[Constants.STEAM_INPUT_MAX_COUNT];
        int controllerCount = SteamInput.GetConnectedControllers(handles);
        if (controllerCount == 0)
        {
            InputSourceStatus ready = new(InputSourceState.Ready);
            if (ready != status)
            {
                status = ready;
                publishStatus(status);
            }

            return;
        }

        int dx = 0;
        int dy = 0;
        MouseButtons buttons = MouseButtons.None;
        sbyte wheel = 0;

        for (int index = 0; index < controllerCount; index++)
        {
            InputHandle_t handle = handles[index];
            SteamInput.ActivateActionSet(handle, actionSet);

            InputAnalogActionData_t pointer = SteamInput.GetAnalogActionData(handle, pointerAction);
            if (pointer.bActive != 0)
            {
                dx += (int)MathF.Round(pointer.x);
                dy += (int)MathF.Round(pointer.y);
            }

            buttons |= ReadButtons(handle);
            wheel = AddWheel(wheel, ReadDigital(handle, wheelUp) ? 1 : 0);
            wheel = AddWheel(wheel, ReadDigital(handle, wheelDown) ? -1 : 0);
        }

        if (dx == 0 && dy == 0 && wheel == 0 && buttons == lastButtons)
        {
            InputSourceStatus active = new(InputSourceState.Ready, controllerCount);
            if (active != status)
            {
                status = active;
                publishStatus(status);
            }

            return;
        }

        lastButtons = buttons;
        publishFrame(new MouseInputFrame(ClampToInt16(dx), ClampToInt16(dy), wheel, buttons));
        InputSourceStatus nextStatus = new(InputSourceState.Ready, controllerCount);
        if (nextStatus != status)
        {
            status = nextStatus;
            publishStatus(status);
        }
    }

    private MouseButtons ReadButtons(InputHandle_t handle)
    {
        MouseButtons buttons = MouseButtons.None;
        if (ReadDigital(handle, leftClick))
        {
            buttons |= MouseButtons.Left;
        }

        if (ReadDigital(handle, rightClick))
        {
            buttons |= MouseButtons.Right;
        }

        if (ReadDigital(handle, middleClick))
        {
            buttons |= MouseButtons.Middle;
        }

        if (ReadDigital(handle, backClick))
        {
            buttons |= MouseButtons.Back;
        }

        if (ReadDigital(handle, forwardClick))
        {
            buttons |= MouseButtons.Forward;
        }

        return buttons;
    }

    private static bool ReadDigital(InputHandle_t handle, InputDigitalActionHandle_t action)
    {
        InputDigitalActionData_t data = SteamInput.GetDigitalActionData(handle, action);
        return data.bActive != 0 && data.bState != 0;
    }

    private static short ClampToInt16(int value)
    {
        return (short)Math.Clamp(value, short.MinValue, short.MaxValue);
    }

    private static sbyte AddWheel(sbyte current, int delta)
    {
        return (sbyte)Math.Clamp(current + delta, sbyte.MinValue, sbyte.MaxValue);
    }
}
