using System;
using SteamHidBridge.Protocol;

namespace SteamHidBridge.App.Core;

internal readonly record struct MouseInputFrame(
    short PointerDeltaX,
    short PointerDeltaY,
    sbyte VerticalWheel,
    MouseButtons Buttons);

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
    ConnectFailed,
    FrameEncodeFailed,
    WriteFailed
}

internal readonly record struct OutputStatus(
    OutputConnectionState State,
    string? Endpoint = null,
    OutputError Error = OutputError.None,
    MouseInputFrame? LastFrame = null);

internal interface IMouseOutputTarget : IDisposable
{
    OutputStatus GetStatus();
    void Refresh();
    void WriteFrame(MouseInputFrame frame);
    void ResetState();
}
