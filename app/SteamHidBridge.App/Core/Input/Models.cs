using SteamHidBridge.App.Configuration;
using SteamHidBridge.Protocol;

namespace SteamHidBridge.App.Core.Input;

internal interface IMouseInputConsumer
{
    void Consume(MouseInputFrame frame);
}

internal interface IOutputStatusProvider
{
    OutputStatus Status { get; }

    void Refresh();
}

internal readonly record struct MouseInputFrame(
    short PointerDeltaX,
    short PointerDeltaY,
    sbyte VerticalWheel,
    MouseButtons Buttons);

internal sealed record MouseInputStatistics(
    long EventCount,
    long PreviewCount,
    double EventsPerSecond,
    double PreviewFramesPerSecond);

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

internal readonly record struct OutputStatus(
    BridgeOutputMode Mode,
    OutputConnectionState State,
    string? Endpoint = null,
    OutputError Error = OutputError.None);
