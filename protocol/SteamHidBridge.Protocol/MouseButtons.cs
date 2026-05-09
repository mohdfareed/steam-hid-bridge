using System;

namespace SteamHidBridge.Protocol;

[Flags]
public enum MouseButtons : ushort
{
    None = 0,
    Left = 1 << 0,
    Right = 1 << 1,
    Middle = 1 << 2,
    Back = 1 << 3,
    Forward = 1 << 4
}
