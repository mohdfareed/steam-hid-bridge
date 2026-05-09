using System;

namespace SteamHidBridge.App.Startup;

public sealed record BridgeLaunchOptions(string ProfileId)
{
    public const string DefaultProfileId = "default";

    public static BridgeLaunchOptions Parse(string[] args)
    {
        for (int index = 0; index < args.Length; index++)
        {
            if (string.Equals(args[index], "--profile", StringComparison.OrdinalIgnoreCase) &&
                index + 1 < args.Length)
            {
                return new BridgeLaunchOptions(args[index + 1]);
            }

            const string profilePrefix = "--profile=";
            if (args[index].StartsWith(profilePrefix, StringComparison.OrdinalIgnoreCase))
            {
                return new BridgeLaunchOptions(args[index][profilePrefix.Length..]);
            }
        }

        return new BridgeLaunchOptions(DefaultProfileId);
    }
}
