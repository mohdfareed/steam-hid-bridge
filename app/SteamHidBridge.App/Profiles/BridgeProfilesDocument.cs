using System.Collections.Generic;

namespace SteamHidBridge.App.Profiles;

public sealed class BridgeProfilesDocument
{
    public List<BridgeProfile> Profiles { get; init; } = [];
}
