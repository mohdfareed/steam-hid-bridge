using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;

namespace SteamHidBridge.App.Platform;

internal sealed class SteamInputConfigForcer
{
    private ulong? requestedAppId;

    public ulong? AppId { get; } = DetectAppId();

    public bool TrySetForced(bool shouldForce)
    {
        if (AppId is not ulong appId)
        {
            return false;
        }

        if (shouldForce)
        {
            if (requestedAppId == appId)
            {
                return false;
            }

            requestedAppId = appId;
            _ = TryOpenForceUrl(appId);
            return true;
        }

        if (requestedAppId is null)
        {
            return false;
        }

        requestedAppId = null;
        _ = TryOpenForceUrl(0);
        return true;
    }

    public void Reset()
    {
        _ = TrySetForced(false);
    }

    private static ulong? DetectAppId()
    {
        foreach (string variable in new[] { "SteamAppId", "SteamGameId" })
        {
            string? value = Environment.GetEnvironmentVariable(variable);
            if (TryParseAppId(value, out ulong appId))
            {
                return appId;
            }
        }

        return null;
    }

    private static bool TryParseAppId(string? value, out ulong appId)
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

    private static bool TryOpenForceUrl(ulong appId)
    {
        try
        {
            using Process? _ = Process.Start(new ProcessStartInfo
            {
                FileName = $"steam://forceinputappid/{appId}",
                UseShellExecute = true
            });
            return true;
        }
        catch (Exception ex) when (ex is InvalidOperationException or Win32Exception)
        {
            return false;
        }
    }
}
