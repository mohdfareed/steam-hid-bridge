using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

namespace SteamHidBridge.App.Infrastructure;

public static partial class VirtualMouseDriverInstaller
{
    private const uint ScManagerConnect = 0x0001;
    private const uint ServiceQueryStatus = 0x0004;
    private const string ServiceName = "SteamHidBridgeVirtualMouse";

    public static bool IsInstalled()
    {
        nint manager = OpenSCManagerW(null, null, ScManagerConnect);
        if (manager == nint.Zero)
        {
            return false;
        }

        try
        {
            nint service = OpenServiceW(manager, ServiceName, ServiceQueryStatus);
            if (service == nint.Zero)
            {
                return false;
            }

            CloseServiceHandle(service);
            return true;
        }
        finally
        {
            CloseServiceHandle(manager);
        }
    }

    public static void StartInstall()
    {
        string scriptPath = FindInstallScript();
        ProcessStartInfo startInfo = new()
        {
            FileName = "powershell.exe",
            Arguments = $"-NoExit -ExecutionPolicy Bypass -File \"{scriptPath}\" -EnableTestSigning -Sign",
            UseShellExecute = true,
            Verb = "runas",
            WorkingDirectory = Path.GetDirectoryName(scriptPath) ?? AppContext.BaseDirectory
        };

        _ = Process.Start(startInfo);
    }

    private static string FindInstallScript()
    {
        string packagedPath = Path.Combine(AppContext.BaseDirectory, "driver", "install.ps1");
        if (File.Exists(packagedPath))
        {
            return packagedPath;
        }

        DirectoryInfo? current = new(AppContext.BaseDirectory);
        while (current is not null)
        {
            string repoPath = Path.Combine(current.FullName, "scripts", "driver", "install.ps1");
            if (File.Exists(repoPath))
            {
                return repoPath;
            }

            current = current.Parent;
        }

        throw new FileNotFoundException("Could not find driver installer script.", packagedPath);
    }

    [LibraryImport("advapi32.dll", EntryPoint = "OpenSCManagerW", StringMarshalling = StringMarshalling.Utf16, SetLastError = true)]
    private static partial nint OpenSCManagerW(string? machineName, string? databaseName, uint desiredAccess);

    [LibraryImport("advapi32.dll", EntryPoint = "OpenServiceW", StringMarshalling = StringMarshalling.Utf16, SetLastError = true)]
    private static partial nint OpenServiceW(nint serviceControlManager, string serviceName, uint desiredAccess);

    [LibraryImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool CloseServiceHandle(nint handle);
}
