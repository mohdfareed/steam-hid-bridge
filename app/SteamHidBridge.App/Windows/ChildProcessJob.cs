using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using SteamHidBridge.App.Infrastructure;

namespace SteamHidBridge.App.Windows;

public sealed partial class ChildProcessJob : IDisposable
{
    private IntPtr handle;
    private bool disposed;

    public ChildProcessJob()
    {
        handle = CreateJobObjectW(IntPtr.Zero, null);
        if (handle == IntPtr.Zero)
        {
            throw new Win32Exception(Marshal.GetLastPInvokeError(), "Could not create child process job.");
        }

        JobObjectExtendedLimitInformation info = new()
        {
            BasicLimitInformation =
            {
                LimitFlags = JobObjectLimitFlags.KillOnJobClose
            }
        };

        if (!SetInformationJobObject(
            handle,
            JobObjectInfoClass.ExtendedLimitInformation,
            ref info,
            Marshal.SizeOf<JobObjectExtendedLimitInformation>()))
        {
            throw new Win32Exception(Marshal.GetLastPInvokeError(), "Could not configure child process job.");
        }
    }

    public bool TryAdd(Process process)
    {
        if (disposed || process.HasExited)
        {
            return false;
        }

        if (AssignProcessToJobObject(handle, process.Handle))
        {
            return true;
        }

        AppLog.Write($"job-assign-failed pid={process.Id} error={Marshal.GetLastPInvokeError()}");
        return false;
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        if (handle != IntPtr.Zero)
        {
            _ = CloseHandle(handle);
            handle = IntPtr.Zero;
        }

        disposed = true;
    }

    [LibraryImport("kernel32.dll", EntryPoint = "CreateJobObjectW", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    private static partial IntPtr CreateJobObjectW(IntPtr jobAttributes, string? name);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetInformationJobObject(
        IntPtr job,
        JobObjectInfoClass infoClass,
        ref JobObjectExtendedLimitInformation info,
        int infoLength);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool AssignProcessToJobObject(IntPtr job, IntPtr process);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool CloseHandle(IntPtr handle);

    private enum JobObjectInfoClass
    {
        ExtendedLimitInformation = 9
    }

    [Flags]
    private enum JobObjectLimitFlags : uint
    {
        KillOnJobClose = 0x00002000
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct JobObjectBasicLimitInformation
    {
        public long PerProcessUserTimeLimit;
        public long PerJobUserTimeLimit;
        public JobObjectLimitFlags LimitFlags;
        public nuint MinimumWorkingSetSize;
        public nuint MaximumWorkingSetSize;
        public uint ActiveProcessLimit;
        public nuint Affinity;
        public uint PriorityClass;
        public uint SchedulingClass;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct IoCounters
    {
        public ulong ReadOperationCount;
        public ulong WriteOperationCount;
        public ulong OtherOperationCount;
        public ulong ReadTransferCount;
        public ulong WriteTransferCount;
        public ulong OtherTransferCount;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct JobObjectExtendedLimitInformation
    {
        public JobObjectBasicLimitInformation BasicLimitInformation;
        public IoCounters IoInfo;
        public nuint ProcessMemoryLimit;
        public nuint JobMemoryLimit;
        public nuint PeakProcessMemoryUsed;
        public nuint PeakJobMemoryUsed;
    }
}
