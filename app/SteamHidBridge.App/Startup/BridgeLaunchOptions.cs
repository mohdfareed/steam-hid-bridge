using System;

namespace SteamHidBridge.App.Startup;

public sealed record BridgeLaunchOptions(string ProfileId, bool LaunchGame)
{
    public static BridgeLaunchOptions Parse(string[] args)
    {
        string profileId = string.Empty;
        bool launchGame = false;

        for (int index = 0; index < args.Length; index++)
        {
            if (string.Equals(args[index], "--profile", StringComparison.OrdinalIgnoreCase) && index + 1 < args.Length)
            {
                profileId = args[index + 1].Trim();
                index++;
                continue;
            }

            if (TryReadValue(args[index], "--profile=", out string profileValue))
            {
                profileId = profileValue.Trim();
                continue;
            }

            if (string.Equals(args[index], "--launch", StringComparison.OrdinalIgnoreCase))
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
