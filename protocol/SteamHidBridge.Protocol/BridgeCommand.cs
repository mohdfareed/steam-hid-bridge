namespace SteamHidBridge.Protocol;

public enum BridgeCommand : byte
{
    Ping = 0x01,
    HidInput = 0x10,
    Reset = 0x20,
    Diagnostics = 0x30
}
