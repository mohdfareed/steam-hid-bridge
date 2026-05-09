using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace SteamHidBridge.App.Windows;

public sealed partial class Win32ForegroundWindowService : IForegroundWindowService
{
    public ForegroundWindowSnapshot GetForegroundWindow()
    {
        IntPtr window = GetForegroundWindowHandle();
        if (window == IntPtr.Zero)
        {
            return new ForegroundWindowSnapshot(null, 0);
        }

        _ = GetWindowThreadProcessId(window, out uint processId);
        if (processId == 0)
        {
            return new ForegroundWindowSnapshot(null, 0);
        }

        try
        {
            using Process process = Process.GetProcessById((int)processId);
            return new ForegroundWindowSnapshot(process.ProcessName + ".exe", (int)processId);
        }
        catch (ArgumentException)
        {
            return new ForegroundWindowSnapshot(null, (int)processId);
        }
        catch (InvalidOperationException)
        {
            return new ForegroundWindowSnapshot(null, (int)processId);
        }
    }

    [LibraryImport("user32.dll", EntryPoint = "GetForegroundWindow")]
    private static partial IntPtr GetForegroundWindowHandle();

    [LibraryImport("user32.dll")]
    private static partial uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);
}
