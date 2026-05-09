using System;
using System.Windows;
using SteamHidBridge.App.Views;
using Drawing = System.Drawing;
using Forms = System.Windows.Forms;

namespace SteamHidBridge.App.Infrastructure;

public sealed class TrayIconHost : IDisposable
{
    private readonly MainWindow window;
    private readonly Action exit;
    private readonly Forms.NotifyIcon notifyIcon;
    private bool disposed;

    public TrayIconHost(MainWindow window, string profileId, Action exit)
    {
        this.window = window;
        this.exit = exit;

        Forms.ContextMenuStrip menu = new();
        _ = menu.Items.Add(new Forms.ToolStripMenuItem($"Profile: {profileId}") { Enabled = false });
        _ = menu.Items.Add(new Forms.ToolStripMenuItem("Open", null, (_, _) => ShowWindow()));
        _ = menu.Items.Add(new Forms.ToolStripSeparator());
        _ = menu.Items.Add(new Forms.ToolStripMenuItem("Exit", null, (_, _) => this.exit()));

        notifyIcon = new Forms.NotifyIcon
        {
            Icon = Drawing.SystemIcons.Application,
            Text = BuildText(profileId),
            ContextMenuStrip = menu,
            Visible = true
        };
        notifyIcon.DoubleClick += (_, _) => ShowWindow();
    }

    public void ShowWindow()
    {
        if (!window.Dispatcher.CheckAccess())
        {
            window.Dispatcher.Invoke(ShowWindow);
            return;
        }

        window.Show();
        if (window.WindowState == WindowState.Minimized)
        {
            window.WindowState = WindowState.Normal;
        }

        _ = window.Activate();
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        notifyIcon.Visible = false;
        notifyIcon.Dispose();
        disposed = true;
    }

    private static string BuildText(string profileId)
    {
        string text = $"Steam HID Bridge - {profileId}";
        return text.Length <= 63 ? text : text[..63];
    }
}
