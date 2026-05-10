using System.Diagnostics;
using System.IO;

namespace SteamHidBridge.App.Infrastructure;

public static class AppDataFolder
{
    public static void Open()
    {
        _ = Directory.CreateDirectory(AppDataPaths.RootDirectory);
        Process.Start(new ProcessStartInfo
        {
            FileName = AppDataPaths.RootDirectory,
            UseShellExecute = true
        });
    }
}
