using SteamHidBridge.Protocol;

namespace SteamHidBridge.App.Steam;

public static class SteamInputReader
{
    public static string StatusText => "Steam Input API spike pending";

    public static bool TryReadLatest(out HidInputReport report)
    {
        report = default;
        return false;
    }
}
