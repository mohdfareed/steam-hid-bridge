using System;
using SteamHidBridge.Protocol;

namespace SteamHidBridge.App.Core;

internal sealed class MouseInputRouter(
    Action<MouseInputFrame> previewFrame,
    Action<MouseInputFrame> forwardingConsumer,
    Func<bool> isForwardingEnabled)
{
    public void Publish(MouseInputFrame frame)
    {
        if (isForwardingEnabled())
        {
            forwardingConsumer(frame);
        }

        previewFrame(frame);
    }
}

// Models

internal readonly record struct MouseInputFrame(
    short PointerDeltaX,
    short PointerDeltaY,
    sbyte VerticalWheel,
    MouseButtons Buttons);

internal enum InputSourceState
{
    Inactive,
    Starting,
    Ready,
    Error
}

internal readonly record struct InputSourceStatus(
    InputSourceState State,
    int ControllerCount = 0,
    string? Detail = null);

internal enum OutputConnectionState
{
    Idle,
    Disconnected,
    Connected,
    Error
}

internal enum OutputError
{
    None,
    FrameEncodeFailed,
    WriteFailed,
    UnknownMode
}

internal readonly record struct BoardOutputStatus(
    OutputConnectionState State,
    string? Endpoint = null,
    OutputError Error = OutputError.None);
