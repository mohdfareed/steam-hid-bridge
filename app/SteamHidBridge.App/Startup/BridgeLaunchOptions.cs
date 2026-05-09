using System;
using System.Globalization;

namespace SteamHidBridge.App.Startup;

public sealed record BridgeLaunchOptions(string ProfileId, bool LaunchGame, ulong? SteamAppId)
{
    public static BridgeLaunchOptions Parse(string[] args)
    {
        string profileId = string.Empty;
        bool launchGame = false;
        ulong? steamAppId = null;

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

            if (string.Equals(args[index], "--steam-app-id", StringComparison.OrdinalIgnoreCase) &&
                index + 1 < args.Length &&
                TryParseAppId(args[index + 1], out ulong appId))
            {
                steamAppId = appId;
                index++;
                continue;
            }

            if (TryReadValue(args[index], "--steam-app-id=", out string appIdValue) &&
                TryParseAppId(appIdValue, out appId))
            {
                steamAppId = appId;
                continue;
            }

            if (string.Equals(args[index], "--launch", StringComparison.OrdinalIgnoreCase))
            {
                launchGame = true;
            }
        }

        return new BridgeLaunchOptions(profileId, launchGame, steamAppId);
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

    private static bool TryParseAppId(string value, out ulong appId)
    {
        if (ulong.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out appId) && appId != 0)
        {
            return true;
        }

        if (long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out long signedAppId) &&
            signedAppId < 0 &&
            signedAppId >= int.MinValue)
        {
            appId = unchecked((uint)(int)signedAppId);
            return appId != 0;
        }

        appId = 0;
        return false;
    }
}
