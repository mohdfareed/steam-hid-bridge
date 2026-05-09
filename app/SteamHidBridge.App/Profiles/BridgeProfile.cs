using System.Collections.Generic;

namespace SteamHidBridge.App.Profiles;

public sealed record BridgeProfile(
    string Id,
    string DisplayName,
    IReadOnlyList<string> TargetProcesses,
    bool AutoEnableWhenForeground,
    bool AutoExitWhenTargetExits)
{
    public static BridgeProfile Default { get; } = new(
        Id: "default",
        DisplayName: "Default",
        TargetProcesses: [],
        AutoEnableWhenForeground: false,
        AutoExitWhenTargetExits: false);
}
