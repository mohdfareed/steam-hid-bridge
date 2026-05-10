using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SteamHidBridge.App.Core.Input;
using SteamHidBridge.App.Platform.App;
using SteamHidBridge.Protocol;
using Steamworks;

namespace SteamHidBridge.App.Platform.Steam;

public sealed class SteamInputMouseEmitter(Action<MouseInputFrame> publishFrame, Action<string, bool> publishStatus) : IDisposable
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
    private string statusText = "Steam Input inactive.";

    public string StatusText
    {
        get
        {
            lock (syncLock)
            {
                return statusText;
            }
        }
    }

    public void SetEnabled(bool value)
    {
        lock (syncLock)
        {
            enabled = value;
            statusText = value ? "Steam Input starting." : "Steam Input inactive.";
        }

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
            pollTask?.Wait(TimeSpan.FromSeconds(1));
        }
        catch (AggregateException ex) when (ex.InnerExceptions.All(static exception => exception is OperationCanceledException))
        {
        }
        catch (AggregateException ex)
        {
            AppLog.WriteException("steam-input-dispose-failed", ex);
        }

        if (initialized)
        {
            try
            {
                SteamInput.Shutdown();
                SteamAPI.Shutdown();
            }
            catch (Exception ex)
            {
                AppLog.WriteException("steam-input-shutdown-failed", ex);
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
            string error = "";
            ESteamAPIInitResult initResult = SteamAPI.InitEx(out error);
            if (initResult != ESteamAPIInitResult.k_ESteamAPIInitResult_OK)
            {
                SetStatus($"Steam Input unavailable: {initResult}. {error}".Trim(), isError: true);
                return false;
            }

            if (!SteamInput.SetInputActionManifestFilePath(manifestPath))
            {
                SetStatus($"Steam Input unavailable: could not load action manifest {manifestPath}", isError: true);
                return false;
            }

            if (!SteamInput.Init(false))
            {
                SetStatus("Steam Input unavailable: SteamInput.Init failed.", isError: true);
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
            SetStatus("Steam Input ready.");
            return true;
        }
        catch (Exception ex) when (ex is DllNotFoundException or InvalidOperationException or EntryPointNotFoundException)
        {
            AppLog.WriteException("steam-input-init-failed", ex);
            SetStatus($"Steam Input unavailable: {ex.Message}", isError: true);
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
            SetStatus("Steam Input ready; no controllers.");
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
            SetStatus($"Steam Input ready; {controllerCount} controller(s).");
            return;
        }

        lastButtons = buttons;
        publishFrame(new MouseInputFrame(ClampToInt16(dx), ClampToInt16(dy), wheel, buttons));
        SetStatus($"Steam Input ready; {controllerCount} controller(s).");
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

    private void SetStatus(string value, bool isError = false)
    {
        bool changed;
        lock (syncLock)
        {
            changed = !string.Equals(statusText, value, StringComparison.Ordinal);
            statusText = value;
        }

        if (changed || isError)
        {
            publishStatus(value, isError);
        }
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
