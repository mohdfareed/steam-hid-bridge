using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;

namespace SteamHidBridge.App.Steam;

public sealed class SteamInputConfigForcer
{
    private ulong? requestedAppId;

    public ulong? AppId { get; } = DetectAppId();

    public string StatusText => AppId is ulong appId
        ? $"Steam config force available for appid {appId}"
        : "Steam config force unavailable; no Steam app id was detected";

    public bool TrySetForced(bool shouldForce, out string message)
    {
        message = string.Empty;
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
            if (TryOpenForceUrl(appId, out message))
            {
                message = $"Forced Steam Input config appid {appId}.";
            }

            return true;
        }

        if (requestedAppId is null)
        {
            return false;
        }

        requestedAppId = null;
        if (TryOpenForceUrl(0, out message))
        {
            message = "Reset Steam Input config forcing.";
        }

        return true;
    }

    public void Reset()
    {
        _ = TrySetForced(false, out _);
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

    private static bool TryOpenForceUrl(ulong appId, out string message)
    {
        try
        {
            using Process? _ = Process.Start(new ProcessStartInfo
            {
                FileName = $"steam://forceinputappid/{appId}",
                UseShellExecute = true
            });
            message = string.Empty;
            return true;
        }
        catch (Exception ex) when (ex is InvalidOperationException or Win32Exception)
        {
            message = $"Could not force Steam Input config: {ex.Message}";
            return false;
        }
    }
}
