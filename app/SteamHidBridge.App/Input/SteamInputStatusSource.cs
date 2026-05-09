namespace SteamHidBridge.App.Input;

public sealed class SteamInputStatusSource : IInputSourceStatus
{
    public InputSourceStatus GetStatus()
    {
        return new InputSourceStatus(
            IsAvailable: false,
            Detail: "Steam Input API spike pending");
    }
}
