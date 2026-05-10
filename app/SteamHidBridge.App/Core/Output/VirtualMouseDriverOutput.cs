using System;
using System.Runtime.InteropServices;
using System.Threading;
using SteamHidBridge.App.Core.Input;
using SteamHidBridge.Protocol;

namespace SteamHidBridge.App.Core.Output;

public sealed partial class VirtualMouseDriverOutput : IMouseInputConsumer, IOutputStatusProvider, IDisposable
{
    private const uint DigcfPresent = 0x00000002;
    private const uint DigcfDeviceInterface = 0x00000010;
    private const uint GenericWrite = 0x40000000;
    private const uint FileShareRead = 0x00000001;
    private const uint FileShareWrite = 0x00000002;
    private const uint OpenExisting = 3;
    private const uint IoctlSubmitMouseReport = 0x00222004;
    private static readonly Guid DeviceInterfaceGuid = new("51B576E7-9E82-4E04-9F16-74FC961EF0A5");
    private readonly Lock syncLock = new();
    private nint deviceHandle = -1;
    private string statusText = "Driver disconnected";
    private bool isDisposed;

    public string StatusText
    {
        get
        {
            lock (syncLock)
            {
                return statusText;
            }
        }
    }

    public void Refresh()
    {
        lock (syncLock)
        {
            if (!isDisposed)
            {
                _ = GetOrOpenDevice();
            }
        }
    }

    public void Consume(MouseInputFrame frame)
    {
        lock (syncLock)
        {
            if (isDisposed)
            {
                return;
            }

            nint handle = GetOrOpenDevice();
            if (handle == -1)
            {
                return;
            }

            MouseReport report = new()
            {
                ReportId = 1,
                Buttons = ToDriverButtons(frame.Buttons),
                X = frame.PointerDeltaX,
                Y = frame.PointerDeltaY,
                Wheel = frame.VerticalWheel
            };

            if (!DeviceIoControl(handle, IoctlSubmitMouseReport, ref report, (uint)Marshal.SizeOf<MouseReport>(), nint.Zero, 0, out _, nint.Zero))
            {
                int error = Marshal.GetLastPInvokeError();
                statusText = $"Driver write failed: {error}";
                CloseDevice();
            }
            else
            {
                statusText = "Driver connected";
            }
        }
    }

    public void Dispose()
    {
        lock (syncLock)
        {
            isDisposed = true;
            CloseDevice();
        }
    }

    private nint GetOrOpenDevice()
    {
        if (deviceHandle != -1)
        {
            return deviceHandle;
        }

        string? path = FindDevicePath();
        if (string.IsNullOrWhiteSpace(path))
        {
            statusText = "Driver disconnected: device not found";
            return -1;
        }

        nint handle = CreateFileW(path, GenericWrite, FileShareRead | FileShareWrite, nint.Zero, OpenExisting, 0, nint.Zero);
        if (handle == -1)
        {
            statusText = $"Driver open failed: {Marshal.GetLastPInvokeError()}";
            return -1;
        }

        deviceHandle = handle;
        statusText = "Driver connected";
        return deviceHandle;
    }

    private static string? FindDevicePath()
    {
        Guid deviceInterfaceGuid = DeviceInterfaceGuid;
        nint deviceInfo = SetupDiGetClassDevsW(ref deviceInterfaceGuid, null, nint.Zero, DigcfDeviceInterface | DigcfPresent);
        if (deviceInfo == -1)
        {
            return null;
        }

        try
        {
            DeviceInterfaceData interfaceData = new()
            {
                Size = Marshal.SizeOf<DeviceInterfaceData>()
            };

            if (!SetupDiEnumDeviceInterfaces(deviceInfo, nint.Zero, ref deviceInterfaceGuid, 0, ref interfaceData))
            {
                return null;
            }

            _ = SetupDiGetDeviceInterfaceDetailW(deviceInfo, ref interfaceData, nint.Zero, 0, out int requiredSize, nint.Zero);
            if (requiredSize <= 0)
            {
                return null;
            }

            nint detailBuffer = Marshal.AllocHGlobal(requiredSize);
            try
            {
                Marshal.WriteInt32(detailBuffer, IntPtr.Size == 8 ? 8 : 6);
                return SetupDiGetDeviceInterfaceDetailW(deviceInfo, ref interfaceData, detailBuffer, requiredSize, out _, nint.Zero)
                    ? Marshal.PtrToStringUni(detailBuffer + 4)
                    : null;
            }
            finally
            {
                Marshal.FreeHGlobal(detailBuffer);
            }
        }
        finally
        {
            _ = SetupDiDestroyDeviceInfoList(deviceInfo);
        }
    }

    private void CloseDevice()
    {
        if (deviceHandle == -1)
        {
            return;
        }

        _ = CloseHandle(deviceHandle);
        deviceHandle = -1;
    }

    private static byte ToDriverButtons(MouseButtons buttons)
    {
        byte value = 0;
        if (buttons.HasFlag(MouseButtons.Left))
        {
            value |= 1 << 0;
        }

        if (buttons.HasFlag(MouseButtons.Right))
        {
            value |= 1 << 1;
        }

        if (buttons.HasFlag(MouseButtons.Middle))
        {
            value |= 1 << 2;
        }

        if (buttons.HasFlag(MouseButtons.Back))
        {
            value |= 1 << 3;
        }

        if (buttons.HasFlag(MouseButtons.Forward))
        {
            value |= 1 << 4;
        }

        return value;
    }

    [LibraryImport("setupapi.dll", EntryPoint = "SetupDiGetClassDevsW", StringMarshalling = StringMarshalling.Utf16, SetLastError = true)]
    private static partial nint SetupDiGetClassDevsW(ref Guid classGuid, string? enumerator, nint hwndParent, uint flags);

    [LibraryImport("setupapi.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetupDiEnumDeviceInterfaces(nint deviceInfoSet, nint deviceInfoData, ref Guid interfaceClassGuid, uint memberIndex, ref DeviceInterfaceData deviceInterfaceData);

    [LibraryImport("setupapi.dll", EntryPoint = "SetupDiGetDeviceInterfaceDetailW", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetupDiGetDeviceInterfaceDetailW(nint deviceInfoSet, ref DeviceInterfaceData deviceInterfaceData, nint deviceInterfaceDetailData, int deviceInterfaceDetailDataSize, out int requiredSize, nint deviceInfoData);

    [LibraryImport("setupapi.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetupDiDestroyDeviceInfoList(nint deviceInfoSet);

    [LibraryImport("kernel32.dll", EntryPoint = "CreateFileW", StringMarshalling = StringMarshalling.Utf16, SetLastError = true)]
    private static partial nint CreateFileW(string fileName, uint desiredAccess, uint shareMode, nint securityAttributes, uint creationDisposition, uint flagsAndAttributes, nint templateFile);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool DeviceIoControl(nint device, uint controlCode, ref MouseReport inBuffer, uint inBufferSize, nint outBuffer, uint outBufferSize, out uint bytesReturned, nint overlapped);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool CloseHandle(nint handle);

    [StructLayout(LayoutKind.Sequential)]
    private struct DeviceInterfaceData
    {
        public int Size;
        public Guid InterfaceClassGuid;
        public uint Flags;
        public nuint Reserved;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    private struct MouseReport
    {
        public byte ReportId;
        public byte Buttons;
        public short X;
        public short Y;
        public sbyte Wheel;
    }
}
