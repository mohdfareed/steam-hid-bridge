using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using SteamHidBridge.App.Core;
using SteamHidBridge.Protocol;

namespace SteamHidBridge.App.Platform.Windows;

internal sealed partial class RawMouseInputWindowHook : IDisposable
{
    private const int UsagePageGenericDesktop = 0x01;
    private const int UsageMouse = 0x02;
    private const int RawInputSink = 0x00000100;
    private const int Input = 0x10000003;
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

    private readonly Action<MouseInputFrame> onFrame;
    private HwndSource? source;
    private MouseButtons buttons;
    private bool isDisposed;

    public RawMouseInputWindowHook(Window window, Action<MouseInputFrame> onFrame)
    {
        this.onFrame = onFrame;
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

        byte[] buffer = new byte[size];
        GCHandle pinned = GCHandle.Alloc(buffer, GCHandleType.Pinned);
        try
        {
            nint bufferPointer = pinned.AddrOfPinnedObject();
            uint read = NativeMethods.GetRawInputData(rawInputHandle, Input, bufferPointer, ref size, (uint)Marshal.SizeOf<RawInputHeader>());
            if (read != size)
            {
                return;
            }

            RawInput rawInput = Marshal.PtrToStructure<RawInput>(bufferPointer);
            RawMouse mouse = rawInput.Mouse;
            MouseButtons nextButtons = UpdateButtons(buttons, mouse.ButtonFlags);
            sbyte wheel = ReadWheel(mouse.ButtonFlags, mouse.ButtonData);

            if (mouse.LastX == 0 && mouse.LastY == 0 && wheel == 0 && nextButtons == buttons)
            {
                return;
            }

            buttons = nextButtons;
            onFrame(new MouseInputFrame(ClampToInt16(mouse.LastX), ClampToInt16(mouse.LastY), wheel, buttons));
        }
        finally
        {
            pinned.Free();
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
    }
}
