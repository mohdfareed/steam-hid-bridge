using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Threading;
using SteamHidBridge.App.Infrastructure;
using SteamHidBridge.App.Profiles;
using SteamHidBridge.App.Runtime;
using SteamHidBridge.App.Startup;
using SteamHidBridge.App.Updates;
using SteamHidBridge.App.ViewModels;
using SteamHidBridge.App.Views;

[assembly: ThemeInfo(ResourceDictionaryLocation.None, ResourceDictionaryLocation.SourceAssembly)]

namespace SteamHidBridge.App;

public partial class App : Application
{
    private TrayIconHost? trayIconHost;
    private MainWindowViewModel? mainWindowViewModel;
    private BridgeRuntime? bridgeRuntime;
    private SteamOverlayHostWindow? overlayHostWindow;
    private ShutdownSignalListener? shutdownSignalListener;
    private bool hideMainWindowToTrayOnClose;
    private bool isExiting;

    public App()
    {
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        try
        {
            base.OnStartup(e);
            AppLog.Write($"startup args=[{string.Join(" ", e.Args.Select(arg => "\"" + arg + "\""))}] base={AppContext.BaseDirectory}");

            BridgeLaunchOptions launchOptions = BridgeLaunchOptions.Parse(e.Args);
            hideMainWindowToTrayOnClose = launchOptions.LaunchGame;
            AppLog.Write($"launch-options profile={launchOptions.ProfileId} launchGame={launchOptions.LaunchGame} steamAppId={launchOptions.SteamAppId}");

            AppSettingsStore settingsStore = AppSettingsStore.LoadDefault();
            AppLog.Write($"settings loaded path={settingsStore.FilePath}");
            AppThemeManager.Apply(settingsStore.Document.General.Theme);

            bridgeRuntime = new BridgeRuntime(launchOptions, []);
            mainWindowViewModel = new MainWindowViewModel(
                launchOptions,
                settingsStore,
                bridgeRuntime,
                AppThemeManager.Apply,
                ConfirmUpdate,
                action => Dispatcher.BeginInvoke(action));
            mainWindowViewModel.ExitRequested += ExitApplication;
            shutdownSignalListener = new ShutdownSignalListener(() => Dispatcher.BeginInvoke(() => ExitApplication(0)));

            MainWindow window = new()
            {
                DataContext = mainWindowViewModel
            };

            window.Closing += OnMainWindowClosing;
            window.Closed += (_, _) => AppLog.Write("main-window closed");
            MainWindow = window;

            trayIconHost = new TrayIconHost(window, mainWindowViewModel.ProfileInstanceText, () => ExitApplication(0));
            if (launchOptions.LaunchGame)
            {
                overlayHostWindow = new SteamOverlayHostWindow();
                overlayHostWindow.Show();
                AppLog.Write("main-window hidden and overlay-host shown for launch mode");
            }
            else
            {
                window.Show();
                _ = window.Activate();
                AppLog.Write("main-window shown");
            }
        }
        catch (Exception ex)
        {
            ShowStartupError(ex);
            Shutdown(1);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        isExiting = true;
        bridgeRuntime?.Dispose();
        overlayHostWindow?.Close();
        trayIconHost?.Dispose();
        shutdownSignalListener?.Dispose();
        AppLog.Write($"exit code={e.ApplicationExitCode}");
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
            AppLog.Write("main-window close requested; exiting interactive app");
            return;
        }

        e.Cancel = true;
        if (sender is Window window)
        {
            window.Hide();
            AppLog.Write("main-window hidden to tray");
        }
    }

    private void ExitApplication(int exitCode)
    {
        isExiting = true;
        Shutdown(exitCode);
    }

    private static void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        AppLog.Write("dispatcher-unhandled-exception");
        ShowStartupError(e.Exception);
        e.Handled = true;
        Current.Shutdown(1);
    }

    private static void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception exception)
        {
            AppLog.WriteException("domain-unhandled-exception", exception);
        }
    }

    private static void ShowStartupError(Exception exception)
    {
        AppLog.WriteException("startup-error", exception);
        _ = MessageBox.Show(
            $"Steam HID Bridge failed to start.\n\n{exception.Message}\n\nDetails were written to:\n{AppLog.FilePath}",
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
}
