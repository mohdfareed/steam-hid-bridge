using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using SteamHidBridge.App.Core;
using SteamHidBridge.Protocol;

namespace SteamHidBridge.App.Platform.Windows;

internal readonly record struct RawMouseObservation(
    MouseInputFrame Frame,
    nint Device,
    string DeviceName);

internal sealed partial class RawMouseInputWindowHook : IDisposable
{
    private const int UsagePageGenericDesktop = 0x01;
    private const int UsageMouse = 0x02;
    private const int RawInputSink = 0x00000100;
    private const int Input = 0x10000003;
    private const int DeviceName = 0x20000007;
    private const int MouseLeftButtonDown = 0x0001;
    private const int MouseLeftButtonUp = 0x0002;
    private const int MouseRightButtonDown = 0x0004;
    private const int MouseRightButtonUp = 0x0008;
    private const int MouseMiddleButtonDown = 0x0010;
    private const int MouseMiddleButtonUp = 0x0020;
    private const int MouseButton4Down = 0x0040;
    private const int MouseButton4Up = 0x0080;
    private const int MouseButton5Down = 0x0100;
    private const int MouseButton5Up = 0x0200;
    private const int MouseWheel = 0x0400;
    private const int WheelDelta = 120;

    private readonly Action<RawMouseObservation> onObservation;
    private byte[] currentInputBuffer = [];
    private byte[] bufferedInputBuffer = [];
    private readonly Dictionary<nint, string> deviceNames = [];
    private HwndSource? source;
    private readonly Dictionary<nint, MouseButtons> buttonStates = [];
    private bool isDisposed;

    public RawMouseInputWindowHook(Window window, Action<RawMouseObservation> onObservation)
    {
        this.onObservation = onObservation;
        window.SourceInitialized += OnSourceInitialized;
    }

    public void Dispose()
    {
        if (isDisposed)
        {
            return;
        }

        isDisposed = true;
        source?.RemoveHook(WndProc);
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        if (sender is not Window window)
        {
            return;
        }

        nint handle = new WindowInteropHelper(window).Handle;
        Register(handle);
        source = HwndSource.FromHwnd(handle);
        source?.AddHook(WndProc);
    }

    private nint WndProc(nint hwnd, int message, nint wParam, nint lParam, ref bool handled)
    {
        if (message == NativeMethods.WmInput)
        {
            ReadInput(lParam);
            DrainBufferedInput();
        }

        return nint.Zero;
    }

    private static void Register(nint window)
    {
        RawInputDevice[] devices =
        [
            new()
            {
                UsagePage = UsagePageGenericDesktop,
                Usage = UsageMouse,
                Flags = RawInputSink,
                Target = window
            }
        ];

        if (!NativeMethods.RegisterRawInputDevices(devices, (uint)devices.Length, (uint)Marshal.SizeOf<RawInputDevice>()))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not register raw mouse input.");
        }
    }

    private void ReadInput(nint rawInputHandle)
    {
        uint size = 0;
        _ = NativeMethods.GetRawInputData(rawInputHandle, Input, nint.Zero, ref size, (uint)Marshal.SizeOf<RawInputHeader>());
        if (size == 0)
        {
            return;
        }

        if (currentInputBuffer.Length < size)
        {
            currentInputBuffer = new byte[size];
        }

        GCHandle pinned = GCHandle.Alloc(currentInputBuffer, GCHandleType.Pinned);
        try
        {
            nint bufferPointer = pinned.AddrOfPinnedObject();
            uint read = NativeMethods.GetRawInputData(rawInputHandle, Input, bufferPointer, ref size, (uint)Marshal.SizeOf<RawInputHeader>());
            if (read != size)
            {
                return;
            }

            ProcessRawInput(bufferPointer);
        }
        finally
        {
            pinned.Free();
        }
    }

    private void DrainBufferedInput()
    {
        while (true)
        {
            uint size = 0;
            uint result = NativeMethods.GetRawInputBuffer(nint.Zero, ref size, (uint)Marshal.SizeOf<RawInputHeader>());
            if (result == uint.MaxValue || size == 0)
            {
                return;
            }

            if (bufferedInputBuffer.Length < size)
            {
                bufferedInputBuffer = new byte[size];
            }

            GCHandle pinned = GCHandle.Alloc(bufferedInputBuffer, GCHandleType.Pinned);
            try
            {
                nint current = pinned.AddrOfPinnedObject();
                uint bufferSize = size;
                uint count = NativeMethods.GetRawInputBuffer(current, ref bufferSize, (uint)Marshal.SizeOf<RawInputHeader>());
                if (count == uint.MaxValue)
                {
                    return;
                }

                for (uint index = 0; index < count; index++)
                {
                    ProcessRawInput(current);
                    RawInput rawInput = Marshal.PtrToStructure<RawInput>(current);
                    current += AlignRawInputSize(rawInput.Header.Size);
                }
            }
            finally
            {
                pinned.Free();
            }
        }
    }

    private void ProcessRawInput(nint bufferPointer)
    {
        RawInput rawInput = Marshal.PtrToStructure<RawInput>(bufferPointer);
        RawMouse mouse = rawInput.Mouse;
        nint device = rawInput.Header.Device;
        MouseButtons currentButtons = buttonStates.TryGetValue(device, out MouseButtons value) ? value : MouseButtons.None;
        MouseButtons nextButtons = UpdateButtons(currentButtons, mouse.ButtonFlags);
        sbyte wheel = ReadWheel(mouse.ButtonFlags, mouse.ButtonData);

        if (mouse.LastX == 0 && mouse.LastY == 0 && wheel == 0 && nextButtons == currentButtons)
        {
            return;
        }

        buttonStates[device] = nextButtons;
        onObservation(new RawMouseObservation(
            new MouseInputFrame(ClampToInt16(mouse.LastX), ClampToInt16(mouse.LastY), wheel, nextButtons),
            device,
            GetDeviceName(device)));
    }

    private string GetDeviceName(nint device)
    {
        if (device == nint.Zero)
        {
            return "Unknown device";
        }

        if (deviceNames.TryGetValue(device, out string? cached))
        {
            return cached;
        }

        uint size = 0;
        _ = NativeMethods.GetRawInputDeviceInfo(device, DeviceName, nint.Zero, ref size);
        if (size == 0)
        {
            return deviceNames[device] = "Unknown device";
        }

        nint buffer = Marshal.AllocHGlobal((int)(size * sizeof(char)));
        try
        {
            uint result = NativeMethods.GetRawInputDeviceInfo(device, DeviceName, buffer, ref size);
            string resolved = result == uint.MaxValue
                ? "Unknown device"
                : Marshal.PtrToStringUni(buffer) ?? "Unknown device";
            deviceNames[device] = resolved;
            return resolved;
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    private static MouseButtons UpdateButtons(MouseButtons current, ushort flags)
    {
        current = SetButton(current, flags, MouseLeftButtonDown, MouseLeftButtonUp, MouseButtons.Left);
        current = SetButton(current, flags, MouseRightButtonDown, MouseRightButtonUp, MouseButtons.Right);
        current = SetButton(current, flags, MouseMiddleButtonDown, MouseMiddleButtonUp, MouseButtons.Middle);
        current = SetButton(current, flags, MouseButton4Down, MouseButton4Up, MouseButtons.Back);
        return SetButton(current, flags, MouseButton5Down, MouseButton5Up, MouseButtons.Forward);
    }

    private static MouseButtons SetButton(MouseButtons current, ushort flags, int downFlag, int upFlag, MouseButtons button)
    {
        if ((flags & downFlag) != 0)
        {
            current |= button;
        }

        if ((flags & upFlag) != 0)
        {
            current &= ~button;
        }

        return current;
    }

    private static sbyte ReadWheel(ushort flags, ushort data)
    {
        if ((flags & MouseWheel) == 0)
        {
            return 0;
        }

        short signedData = unchecked((short)data);
        int notches = signedData / WheelDelta;
        return notches > sbyte.MaxValue ? sbyte.MaxValue : notches < sbyte.MinValue ? sbyte.MinValue : (sbyte)notches;
    }

    private static short ClampToInt16(int value)
    {
        return value > short.MaxValue ? short.MaxValue : value < short.MinValue ? short.MinValue : (short)value;
    }

    private static int AlignRawInputSize(uint size)
    {
        int alignment = IntPtr.Size;
        return (int)((size + (uint)(alignment - 1)) & ~((uint)alignment - 1));
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RawInputDevice
    {
        public ushort UsagePage;
        public ushort Usage;
        public uint Flags;
        public nint Target;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RawInput
    {
        public RawInputHeader Header;
        public RawMouse Mouse;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RawInputHeader
    {
        public uint Type;
        public uint Size;
        public nint Device;
        public nint WParam;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RawMouse
    {
        public ushort Flags;
        public ushort Buttons;
        public ushort ButtonFlags;
        public ushort ButtonData;
        public uint RawButtons;
        public int LastX;
        public int LastY;
        public uint ExtraInformation;
    }

    private static partial class NativeMethods
    {
        public const int WmInput = 0x00FF;

        [LibraryImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static partial bool RegisterRawInputDevices(
            [In] RawInputDevice[] rawInputDevices,
            uint deviceCount,
            uint size);

        [LibraryImport("user32.dll", SetLastError = true)]
        public static partial uint GetRawInputData(
            nint rawInput,
            uint command,
            nint data,
            ref uint size,
            uint headerSize);

        [LibraryImport("user32.dll", SetLastError = true)]
        public static partial uint GetRawInputBuffer(
            nint data,
            ref uint size,
            uint headerSize);

        [LibraryImport("user32.dll", EntryPoint = "GetRawInputDeviceInfoW", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
        public static partial uint GetRawInputDeviceInfo(
            nint device,
            uint command,
            nint data,
            ref uint size);
    }
}
