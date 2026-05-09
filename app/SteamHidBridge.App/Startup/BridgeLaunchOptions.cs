using System;

namespace SteamHidBridge.App.Startup;

public sealed record BridgeLaunchOptions(string ProfileId, bool LaunchGame)
{
    public const string DefaultProfileId = "default";

    public static BridgeLaunchOptions Parse(string[] args)
    {
        string profileId = DefaultProfileId;
        bool launchGame = false;

        for (int index = 0; index < args.Length; index++)
        {
            if ((string.Equals(args[index], "--profile", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(args[index], "--game", StringComparison.OrdinalIgnoreCase)) &&
                index + 1 < args.Length)
            {
                profileId = args[index + 1];
                index++;
                continue;
            }

            if (TryReadValue(args[index], "--profile=", out string profileValue) ||
                TryReadValue(args[index], "--game=", out profileValue))
            {
                profileId = profileValue;
                continue;
            }

            if (string.Equals(args[index], "--launch", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(args[index], "--launch-game", StringComparison.OrdinalIgnoreCase))
            {
                launchGame = true;
            }
        }

        return new BridgeLaunchOptions(profileId, launchGame);
    }

    private static bool TryReadValue(string arg, string prefix, out string value)
    {
        if (arg.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            value = arg[prefix.Length..];
            return true;
        }

        value = string.Empty;
        return false;
    }
}
