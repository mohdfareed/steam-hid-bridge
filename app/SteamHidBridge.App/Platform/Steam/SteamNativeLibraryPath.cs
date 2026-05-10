using System;
using System.IO;
using System.Runtime.InteropServices;
using SteamHidBridge.App.Platform.App;

namespace SteamHidBridge.App.Platform.Steam;

public static partial class SteamNativeLibraryPath
{
    public static void Configure()
    {
        string steamDirectory = Path.Combine(AppContext.BaseDirectory, "Steam");
        if (!Directory.Exists(steamDirectory))
        {
            AppLog.Write($"steam native directory missing path={steamDirectory}");
            return;
        }

        if (!SetDllDirectory(steamDirectory))
        {
            int error = Marshal.GetLastPInvokeError();
            AppLog.Write($"steam native directory registration failed path={steamDirectory} error={error}");
            return;
        }

        AppLog.Write($"steam native directory registered path={steamDirectory}");
    }

    [LibraryImport("kernel32.dll", EntryPoint = "SetDllDirectoryW", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetDllDirectory(string lpPathName);
}
