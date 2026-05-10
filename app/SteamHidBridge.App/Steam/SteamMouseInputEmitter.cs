using System;
using SteamHidBridge.App.Infrastructure;
using SteamHidBridge.App.Input;
using SteamHidBridge.Protocol;
using Steamworks;

namespace SteamHidBridge.App.Steam;

public sealed class SteamMouseInputEmitter
{
    private const string ActionSetName = "bridge";
    private const string PointerActionName = "pointer";
    private const string LeftButtonActionName = "mouse_left";
    private const string RightButtonActionName = "mouse_right";
    private const string MiddleButtonActionName = "mouse_middle";
    private const string BackButtonActionName = "mouse_back";
    private const string ForwardButtonActionName = "mouse_forward";
    private const string WheelUpActionName = "wheel_up";
    private const string WheelDownActionName = "wheel_down";

    private bool hasAttemptedInit;
    private bool isAvailable;
    private InputActionSetHandle_t actionSetHandle;
    private InputAnalogActionHandle_t pointerActionHandle;
    private InputDigitalActionHandle_t leftButtonActionHandle;
    private InputDigitalActionHandle_t rightButtonActionHandle;
    private InputDigitalActionHandle_t middleButtonActionHandle;
    private InputDigitalActionHandle_t backButtonActionHandle;
    private InputDigitalActionHandle_t forwardButtonActionHandle;
    private InputDigitalActionHandle_t wheelUpActionHandle;
    private InputDigitalActionHandle_t wheelDownActionHandle;

    public string StatusText
    {
        get;
        private set;
    } = "Steam Input not initialized.";

    public bool TryReadLatest(out MouseInputFrame frame)
    {
        EnsureInitialized();

        if (!isAvailable)
        {
            frame = default;
            return false;
        }

        try
        {
            SteamAPI.RunCallbacks();
            SteamInput.RunFrame();

            InputHandle_t[] controllers = new InputHandle_t[Constants.STEAM_INPUT_MAX_COUNT];
            int controllerCount = SteamInput.GetConnectedControllers(controllers);
            if (controllerCount <= 0)
            {
                StatusText = "Steam Input initialized; no controllers connected.";
                frame = default;
                return false;
            }

            InputHandle_t controller = controllers[0];
            SteamInput.ActivateActionSet(controller, actionSetHandle);

            InputAnalogActionData_t pointer = SteamInput.GetAnalogActionData(controller, pointerActionHandle);
            MouseButtons buttons = ReadMouseButtons(controller);
            sbyte wheel = ReadWheel(controller);

            if (pointer.bActive == 0 && buttons == MouseButtons.None && wheel == 0)
            {
                StatusText = $"Steam Input initialized; {controllerCount} controller(s) connected.";
                frame = default;
                return false;
            }

            frame = new MouseInputFrame(
                ClampToInt16(pointer.x),
                ClampToInt16(pointer.y),
                wheel,
                buttons);

            StatusText = $"Steam Input reading {controllerCount} controller(s).";
            return true;
        }
        catch (Exception ex) when (ex is InvalidOperationException or DllNotFoundException or EntryPointNotFoundException)
        {
            isAvailable = false;
            StatusText = $"Steam Input failed: {ex.Message}";
            AppLog.WriteException("steam-input-read-failed", ex);
            frame = default;
            return false;
        }
    }

    public void Shutdown()
    {
        if (!isAvailable)
        {
            return;
        }

        SteamInput.Shutdown();
        SteamAPI.Shutdown();
        isAvailable = false;
        StatusText = "Steam Input shut down.";
    }

    private void EnsureInitialized()
    {
        if (hasAttemptedInit)
        {
            return;
        }

        hasAttemptedInit = true;

        try
        {
            if (!SteamAPI.Init())
            {
                StatusText = "SteamAPI.Init returned false. Launch from Steam, or provide a valid steam_appid.txt for local testing.";
                AppLog.Write(StatusText);
                return;
            }

            if (!SteamInput.Init(true))
            {
                StatusText = "SteamInput.Init returned false.";
                AppLog.Write(StatusText);
                SteamAPI.Shutdown();
                return;
            }

            actionSetHandle = SteamInput.GetActionSetHandle(ActionSetName);
            pointerActionHandle = SteamInput.GetAnalogActionHandle(PointerActionName);
            leftButtonActionHandle = SteamInput.GetDigitalActionHandle(LeftButtonActionName);
            rightButtonActionHandle = SteamInput.GetDigitalActionHandle(RightButtonActionName);
            middleButtonActionHandle = SteamInput.GetDigitalActionHandle(MiddleButtonActionName);
            backButtonActionHandle = SteamInput.GetDigitalActionHandle(BackButtonActionName);
            forwardButtonActionHandle = SteamInput.GetDigitalActionHandle(ForwardButtonActionName);
            wheelUpActionHandle = SteamInput.GetDigitalActionHandle(WheelUpActionName);
            wheelDownActionHandle = SteamInput.GetDigitalActionHandle(WheelDownActionName);

            isAvailable = true;
            StatusText = "Steam Input initialized.";
            AppLog.Write(StatusText);
        }
        catch (Exception ex) when (ex is InvalidOperationException or DllNotFoundException or EntryPointNotFoundException)
        {
            StatusText = $"Steam Input unavailable: {ex.Message}";
            AppLog.WriteException("steam-input-init-failed", ex);
        }
    }

    private MouseButtons ReadMouseButtons(InputHandle_t controller)
    {
        MouseButtons buttons = MouseButtons.None;
        if (IsPressed(controller, leftButtonActionHandle))
        {
            buttons |= MouseButtons.Left;
        }

        if (IsPressed(controller, rightButtonActionHandle))
        {
            buttons |= MouseButtons.Right;
        }

        if (IsPressed(controller, middleButtonActionHandle))
        {
            buttons |= MouseButtons.Middle;
        }

        if (IsPressed(controller, backButtonActionHandle))
        {
            buttons |= MouseButtons.Back;
        }

        if (IsPressed(controller, forwardButtonActionHandle))
        {
            buttons |= MouseButtons.Forward;
        }

        return buttons;
    }

    private sbyte ReadWheel(InputHandle_t controller)
    {
        if (IsPressed(controller, wheelUpActionHandle))
        {
            return 1;
        }

        return IsPressed(controller, wheelDownActionHandle) ? (sbyte)-1 : (sbyte)0;
    }

    private static bool IsPressed(InputHandle_t controller, InputDigitalActionHandle_t actionHandle)
    {
        InputDigitalActionData_t data = SteamInput.GetDigitalActionData(controller, actionHandle);
        return data.bActive != 0 && data.bState != 0;
    }

    private static short ClampToInt16(float value)
    {
        if (value > short.MaxValue)
        {
            return short.MaxValue;
        }

        if (value < short.MinValue)
        {
            return short.MinValue;
        }

        return (short)MathF.Round(value);
    }
}
