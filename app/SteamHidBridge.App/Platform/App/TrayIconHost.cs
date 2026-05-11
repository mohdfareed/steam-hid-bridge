using System;
using System.Windows;
using SteamHidBridge.App.Ui.Views;
using Drawing = System.Drawing;
using Forms = System.Windows.Forms;

namespace SteamHidBridge.App.Platform.App;

internal sealed class TrayIconHost : IDisposable
{
    private readonly MainWindow window;
    private readonly Action exit;
    private readonly Drawing.Icon icon;
    private readonly Forms.NotifyIcon notifyIcon;
    private bool disposed;

    public TrayIconHost(MainWindow window, string instanceText, Action exit)
    {
        this.window = window;
        this.exit = exit;

        Forms.ContextMenuStrip menu = new();
        _ = menu.Items.Add(new Forms.ToolStripMenuItem(instanceText) { Enabled = false });
        _ = menu.Items.Add(new Forms.ToolStripMenuItem("Open", null, (_, _) => ShowWindow()));
        _ = menu.Items.Add(new Forms.ToolStripSeparator());
        _ = menu.Items.Add(new Forms.ToolStripMenuItem("Exit", null, (_, _) => RequestExit()));

        icon = LoadIcon();
        notifyIcon = new Forms.NotifyIcon
        {
            Icon = icon,
            Text = BuildText(instanceText),
            ContextMenuStrip = menu,
            Visible = true
        };
        notifyIcon.DoubleClick += (_, _) => ShowWindow();
    }

    public void ShowWindow()
    {
        if (disposed)
        {
            return;
        }

        if (!window.Dispatcher.CheckAccess())
        {
            _ = window.Dispatcher.BeginInvoke(ShowWindow);
            return;
        }

        if (window.Dispatcher.HasShutdownStarted || window.Dispatcher.HasShutdownFinished)
        {
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
        icon.Dispose();
        disposed = true;
    }

    private void RequestExit()
    {
        if (disposed)
        {
            return;
        }

        if (!window.Dispatcher.CheckAccess())
        {
            _ = window.Dispatcher.BeginInvoke(RequestExit);
            return;
        }

        exit();
    }

    private static Drawing.Icon LoadIcon()
    {
        if (!string.IsNullOrWhiteSpace(Environment.ProcessPath))
        {
            Drawing.Icon? extractedIcon = Drawing.Icon.ExtractAssociatedIcon(Environment.ProcessPath);
            if (extractedIcon is not null)
            {
                return extractedIcon;
            }
        }

        return (Drawing.Icon)Drawing.SystemIcons.Application.Clone();
    }

    private static string BuildText(string instanceText)
    {
        string text = $"Steam HID Bridge - {instanceText}";
        return text.Length <= 63 ? text : text[..63];
    }
}
