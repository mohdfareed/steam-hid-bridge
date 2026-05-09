using System;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace SteamHidBridge.App.Profiles;

public sealed class BridgeProfileStore
{
    private readonly BridgeProfilesDocument document;

    private BridgeProfileStore(BridgeProfilesDocument document)
    {
        this.document = document;
    }

    public static BridgeProfileStore LoadDefault()
    {
        string path = Path.Combine(AppContext.BaseDirectory, "profiles.json");
        if (!File.Exists(path))
        {
            return new BridgeProfileStore(new BridgeProfilesDocument());
        }

        string json = File.ReadAllText(path);
        BridgeProfilesDocument? document = JsonSerializer.Deserialize<BridgeProfilesDocument>(
            json,
            new JsonSerializerOptions(JsonSerializerDefaults.Web));

        return new BridgeProfileStore(document ?? new BridgeProfilesDocument());
    }

    public BridgeProfile Resolve(string profileId)
    {
        BridgeProfile? profile = document.Profiles.FirstOrDefault(
            candidate => string.Equals(candidate.Id, profileId, StringComparison.OrdinalIgnoreCase));

        return profile ?? BridgeProfile.Default with { Id = profileId, DisplayName = profileId };
    }
}
