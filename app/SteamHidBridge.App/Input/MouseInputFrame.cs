using SteamHidBridge.Protocol;

namespace SteamHidBridge.App.Input;

public readonly record struct MouseInputFrame(
    short PointerDeltaX,
    short PointerDeltaY,
    sbyte VerticalWheel,
    MouseButtons Buttons);
