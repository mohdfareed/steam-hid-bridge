using SteamHidBridge.Protocol;

namespace SteamHidBridge.App.Core.Input;

public interface IMouseInputConsumer
{
    void Consume(MouseInputFrame frame);
}

public interface IOutputStatusProvider
{
    string StatusText { get; }

    void Refresh();
}

public readonly record struct MouseInputFrame(
    short PointerDeltaX,
    short PointerDeltaY,
    sbyte VerticalWheel,
    MouseButtons Buttons);

public sealed record MouseInputStatistics(
    long EventCount,
    long PreviewCount,
    double EventsPerSecond,
    double PreviewFramesPerSecond);
