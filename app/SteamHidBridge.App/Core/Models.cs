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
    FrameEncodeFailed,
    WriteFailed
}

internal readonly record struct BoardOutputStatus(
    OutputConnectionState State,
    string? Endpoint = null,
    OutputError Error = OutputError.None);
