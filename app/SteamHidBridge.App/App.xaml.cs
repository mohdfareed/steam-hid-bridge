using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Threading;
using SteamHidBridge.App.Configuration;
using SteamHidBridge.App.Core;
using SteamHidBridge.App.Platform;
using SteamHidBridge.App.Platform.App;
using SteamHidBridge.App.Platform.Steam;
using SteamHidBridge.App.Platform.Windows;
using SteamHidBridge.App.Ui.ViewModels;
using SteamHidBridge.App.Ui.Views;

[assembly: ThemeInfo(ResourceDictionaryLocation.None, ResourceDictionaryLocation.SourceAssembly)]

namespace SteamHidBridge.App;

/// <summary>
/// Starts and owns the WPF application lifetime for Steam HID Bridge.
/// </summary>
public partial class App : Application
{
    private TrayIconHost? trayIconHost;
    private MainWindowViewModel? mainWindowViewModel;
    private BridgeRuntime? bridgeRuntime;
    private SteamInputMouseEmitter? steamInputMouseEmitter;
    private ShutdownSignalListener? shutdownSignalListener;
    private RawMouseInputWindowHook? rawMouseInputWindowHook;
    private bool hideMainWindowToTrayOnClose;
    private bool isExiting;

    /// <summary>
    /// Initializes the application object and subscribes to top-level exception handlers.
    /// </summary>
    public App()
    {
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
    }

    /// <summary>
    /// Builds the runtime graph and shows the main window or tray-hosted profile instance.
    /// </summary>
    /// <param name="e">The startup event arguments.</param>
    protected override void OnStartup(StartupEventArgs e)
    {
        try
        {
            base.OnStartup(e);
            ConfigureLogging();

            BridgeLaunchOptions launchOptions = BridgeLaunchOptions.Parse(e.Args);
            hideMainWindowToTrayOnClose = !string.IsNullOrWhiteSpace(launchOptions.ProfileId);

            try
            {
                AppSettings settings = AppSettingsFile.LoadDefault();
                AppThemeManager.Apply(settings.General.Theme);
                ConfigureSteamLibraryPath();

                bridgeRuntime = new BridgeRuntime(launchOptions, settings.General.BoardPort);
                steamInputMouseEmitter = new SteamInputMouseEmitter(bridgeRuntime.PublishSteamInputMouseInput, bridgeRuntime.SetInputStatus);
                mainWindowViewModel = new MainWindowViewModel(
                    launchOptions,
                    settings,
                    bridgeRuntime,
                        ApplyInputMode,
                        bridgeRuntime.SetOutputMode,
                        bridgeRuntime.SetBoardPort,
                        AppThemeManager.Apply,
                        ConfirmUpdate,
                        action => Dispatcher.BeginInvoke(action));
            }
            catch (InvalidDataException ex)
            {
                _ = MessageBox.Show(
                    ex.Message,
                    "Steam HID Bridge Settings Reset",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                AppSettings settings = new();
                AppThemeManager.Apply(settings.General.Theme);
                ConfigureSteamLibraryPath();

                bridgeRuntime = new BridgeRuntime(launchOptions, settings.General.BoardPort);
                steamInputMouseEmitter = new SteamInputMouseEmitter(bridgeRuntime.PublishSteamInputMouseInput, bridgeRuntime.SetInputStatus);
                mainWindowViewModel = new MainWindowViewModel(
                    launchOptions,
                    settings,
                    bridgeRuntime,
                        ApplyInputMode,
                        bridgeRuntime.SetOutputMode,
                        bridgeRuntime.SetBoardPort,
                        AppThemeManager.Apply,
                        ConfirmUpdate,
                        action => Dispatcher.BeginInvoke(action));
            }

            mainWindowViewModel.ExitRequested += ExitApplication;
            shutdownSignalListener = new ShutdownSignalListener(() => Dispatcher.BeginInvoke(() => ExitApplication(0)));

            MainWindow window = new()
            {
                DataContext = mainWindowViewModel
            };

            window.Closing += OnMainWindowClosing;
            MainWindow = window;

            trayIconHost = new TrayIconHost(window, mainWindowViewModel.TrayText, () => ExitApplication(0));
            rawMouseInputWindowHook = new RawMouseInputWindowHook(window, bridgeRuntime.PublishLegacyMouseInput);
            if (!string.IsNullOrWhiteSpace(launchOptions.ProfileId))
            {
                window.Show();
                window.Hide();
            }
            else
            {
                window.Show();
                _ = window.Activate();
            }
        }
        catch (Exception ex)
        {
            ShowStartupError(ex);
            Shutdown(1);
        }
    }

    /// <summary>
    /// Disposes runtime resources before the process exits.
    /// </summary>
    /// <param name="e">The exit event arguments.</param>
    protected override void OnExit(ExitEventArgs e)
    {
        isExiting = true;

        bridgeRuntime?.Dispose();
        steamInputMouseEmitter?.Dispose();
        trayIconHost?.Dispose();
        shutdownSignalListener?.Dispose();
        rawMouseInputWindowHook?.Dispose();

        base.OnExit(e);
    }

    private void OnMainWindowClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (isExiting)
        {
            return;
        }

        if (!hideMainWindowToTrayOnClose)
        {
            isExiting = true;
            return;
        }

        e.Cancel = true;
        if (sender is Window window)
        {
            window.Hide();
        }
    }

    private void ApplyInputMode(BridgeInputMode inputMode)
    {
        bridgeRuntime?.SetInputMode(inputMode);
        steamInputMouseEmitter?.SetEnabled(inputMode == BridgeInputMode.SteamInputActions);
    }

    private void ExitApplication(int exitCode)
    {
        if (!Dispatcher.CheckAccess())
        {
            _ = Dispatcher.BeginInvoke(() => ExitApplication(exitCode));
            return;
        }

        if (isExiting)
        {
            return;
        }

        isExiting = true;
        Shutdown(exitCode);
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        Trace.TraceError($"dispatcher-unhandled-exception{Environment.NewLine}{e.Exception}");
        e.Handled = true;
        if (!isExiting)
        {
            ShowStartupError(e.Exception);
            ExitApplication(1);
        }
    }

    private static void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception exception)
        {
            Trace.TraceError($"domain-unhandled-exception{Environment.NewLine}{exception}");
        }
    }

    private static void ShowStartupError(Exception exception)
    {
        Trace.TraceError($"startup-error{Environment.NewLine}{exception}");
        _ = MessageBox.Show(
            $"Steam HID Bridge failed to start.\n\n{exception.Message}\n\nDetails were written to:\n{AppDataPaths.AppLogPath}",
            "Steam HID Bridge",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
    }

    private static bool ConfirmUpdate(AppUpdateCheckResult update)
    {
        return MessageBox.Show(
            $"Install {update.LatestVersionText}?\n\nThis will close every Steam HID Bridge instance and any game processes launched by them.",
            "Steam HID Bridge Update",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question) == MessageBoxResult.Yes;
    }

    private static void ConfigureLogging()
    {
        _ = Directory.CreateDirectory(AppDataPaths.LogDirectory);
        Trace.AutoFlush = true;
        Trace.Listeners.Clear();
        _ = Trace.Listeners.Add(new TextWriterTraceListener(AppDataPaths.AppLogPath));
    }

    private static void ConfigureSteamLibraryPath()
    {
        string steamDirectory = Path.Combine(AppContext.BaseDirectory, "Steam");
        if (Directory.Exists(steamDirectory))
        {
            _ = SetDllDirectory(steamDirectory);
        }
    }

    [LibraryImport("kernel32.dll", EntryPoint = "SetDllDirectoryW", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetDllDirectory(string lpPathName);
}
