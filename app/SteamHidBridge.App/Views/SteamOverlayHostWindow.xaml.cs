using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace SteamHidBridge.App.Views;

public partial class SteamOverlayHostWindow : Window
{
    private const int ExtendedWindowStyle = -20;
    private const long Transparent = 0x00000020;
    private const long ToolWindow = 0x00000080;

    public SteamOverlayHostWindow()
    {
        InitializeComponent();
        Left = SystemParameters.VirtualScreenLeft;
        Top = SystemParameters.VirtualScreenTop;
        Width = SystemParameters.VirtualScreenWidth;
        Height = SystemParameters.VirtualScreenHeight;
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        nint handle = new WindowInteropHelper(this).Handle;
        nint style = GetWindowLongPtrW(handle, ExtendedWindowStyle);
        _ = SetWindowLongPtrW(handle, ExtendedWindowStyle, style | (nint)(Transparent | ToolWindow));
    }

    [LibraryImport("user32.dll", EntryPoint = "GetWindowLongPtrW", SetLastError = true)]
    private static partial nint GetWindowLongPtrW(nint window, int index);

    [LibraryImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
    private static partial nint SetWindowLongPtrW(nint window, int index, nint value);
}
