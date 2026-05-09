using System;

namespace SteamHidBridge.App.Updates;

public sealed record AppUpdateCheckResult(
    Version CurrentVersion,
    Version LatestVersion,
    string LatestTag,
    string PackageUrl)
{
    public bool IsUpdateAvailable => LatestVersion > CurrentVersion;

    public string CurrentVersionText => $"v{Format(CurrentVersion)}";

    public string LatestVersionText => $"v{Format(LatestVersion)}";

    private static string Format(Version version)
    {
        return $"{version.Major}.{version.Minor}.{Math.Max(version.Build, 0)}";
    }
}
