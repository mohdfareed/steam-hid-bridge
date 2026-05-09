using System;

namespace SteamHidBridge.Protocol;

[Flags]
public enum KeyboardModifiers : byte
{
    None = 0,
    LeftControl = 1 << 0,
    LeftShift = 1 << 1,
    LeftAlt = 1 << 2,
    LeftGui = 1 << 3,
    RightControl = 1 << 4,
    RightShift = 1 << 5,
    RightAlt = 1 << 6,
    RightGui = 1 << 7
}
