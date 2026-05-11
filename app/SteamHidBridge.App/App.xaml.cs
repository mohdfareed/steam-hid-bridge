using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using SteamHidBridge.App.Configuration;
using SteamHidBridge.App.Core;
using SteamHidBridge.App.Platform;
using SteamHidBridge.App.Platform.App;
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
    private sealed record AppRuntime(
        MainWindowViewModel MainWindowViewModel,
        BridgeSession BridgeSession,
        GeneralSettingsViewModel GeneralSettingsViewModel,
        OutputViewModel OutputViewModel,
        ProfileSettingsViewModel ProfileSettingsViewModel);

    private TrayIconHost? trayIconHost;
    private BridgeSession? bridgeSession;
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
            AppRuntime runtime = CreateRuntime(launchOptions, LoadSettingsWithRecovery());
            bridgeSession = runtime.BridgeSession;
            WireSessionEvents(runtime.BridgeSession, runtime.OutputViewModel);
            runtime.GeneralSettingsViewModel.ExitRequested += ExitApplication;

            if (!string.IsNullOrWhiteSpace(launchOptions.ProfileId))
            {
                _ = runtime.ProfileSettingsViewModel.LaunchRequestedProfileAsync();
            }

            shutdownSignalListener = new ShutdownSignalListener(() => Dispatcher.BeginInvoke(() => ExitApplication(0)));

            MainWindow window = new()
            {
                DataContext = runtime.MainWindowViewModel
            };

            window.Closing += OnMainWindowClosing;
            MainWindow = window;

            trayIconHost = new TrayIconHost(window, runtime.MainWindowViewModel.TrayText, () => ExitApplication(0));
            rawMouseInputWindowHook = new RawMouseInputWindowHook(window, bridgeSession.PublishMouseInput);
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

        bridgeSession?.Dispose();
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

    private static AppSettings LoadSettingsWithRecovery()
    {
        try
        {
            return AppSettingsFile.LoadDefault();
        }
        catch (InvalidDataException ex)
        {
            _ = MessageBox.Show(
                ex.Message,
                "Steam HID Bridge Settings Reset",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return new AppSettings();
        }
    }

    private static AppRuntime CreateRuntime(BridgeLaunchOptions launchOptions, AppSettings settings)
    {
        AppThemeManager.Apply(settings.General.Theme);

        BridgeAppService appService = new(settings);
        BridgeSession session = new(launchOptions, appService.BoardPort);
        ProfileSettingsViewModel profileSettingsViewModel = new(appService, session, launchOptions.ProfileId);
        GeneralSettingsViewModel generalSettingsViewModel = new(
            appService,
            session.SetBoardPort,
            AppThemeManager.Apply,
            ConfirmUpdate);
        OutputViewModel outputViewModel = new();
        MainWindowViewModel mainWindowViewModel = new(
            launchOptions.ProfileId,
            profileSettingsViewModel,
            generalSettingsViewModel,
            outputViewModel);

        return new AppRuntime(
            mainWindowViewModel,
            session,
            generalSettingsViewModel,
            outputViewModel,
            profileSettingsViewModel);
    }

    private void WireSessionEvents(BridgeSession session, OutputViewModel outputViewModel)
    {
        session.MouseInput += frame => Dispatcher.BeginInvoke(() => outputViewModel.PreviewMouseInput(frame));
        session.StatusChanged += status => Dispatcher.BeginInvoke(() => outputViewModel.ApplyRuntimeStatus(status));
        session.ExitRequested += exitCode => Dispatcher.BeginInvoke(() => ExitApplication(exitCode));
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
}
