using System;
using System.Linq;
using System.Windows;
using System.Windows.Threading;
using SteamHidBridge.App.Configuration;
using SteamHidBridge.App.Core.Output;
using SteamHidBridge.App.Core.Runtime;
using SteamHidBridge.App.Platform;
using SteamHidBridge.App.Platform.App;
using SteamHidBridge.App.Platform.Steam;
using SteamHidBridge.App.Platform.Windows;
using SteamHidBridge.App.Ui.ViewModels;
using SteamHidBridge.App.Ui.Views;

[assembly: ThemeInfo(ResourceDictionaryLocation.None, ResourceDictionaryLocation.SourceAssembly)]

namespace SteamHidBridge.App;

public partial class App : Application
{
    private TrayIconHost? trayIconHost;
    private MainWindowViewModel? mainWindowViewModel;
    private BridgeRuntime? bridgeRuntime;
    private MouseOutputRouter? mouseOutputRouter;
    private SteamInputMouseEmitter? steamInputMouseEmitter;
    private ShutdownSignalListener? shutdownSignalListener;
    private RawMouseInputWindowHook? rawMouseInputWindowHook;
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
            hideMainWindowToTrayOnClose = !string.IsNullOrWhiteSpace(launchOptions.ProfileId);
            AppLog.Write($"launch-options profile={launchOptions.ProfileId}");

            AppSettingsLoadResult settingsLoad = AppSettingsStore.LoadDefault();
            AppSettingsStore settingsStore = settingsLoad.Store;
            AppLog.Write($"settings loaded path={settingsStore.FilePath}");
            AppThemeManager.Apply(settingsStore.Document.General.Theme);
            SteamNativeLibraryPath.Configure();

            mouseOutputRouter = new MouseOutputRouter(BridgeOutputMode.Board, settingsStore.Document.General.BoardPort);
            bridgeRuntime = new BridgeRuntime(launchOptions, [mouseOutputRouter]);
            steamInputMouseEmitter = new SteamInputMouseEmitter(bridgeRuntime.PublishSteamInputMouseInput, bridgeRuntime.SetInputStatus);
            mainWindowViewModel = new MainWindowViewModel(
                launchOptions,
                settingsStore,
                bridgeRuntime,
                ApplyInputMode,
                mouseOutputRouter.SetMode,
                mouseOutputRouter.SetBoardPort,
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

            trayIconHost = new TrayIconHost(window, mainWindowViewModel.TrayText, () => ExitApplication(0));
            rawMouseInputWindowHook = new RawMouseInputWindowHook(window, bridgeRuntime.PublishLegacyMouseInput);
            if (!string.IsNullOrWhiteSpace(settingsLoad.WarningMessage))
            {
                ShowSettingsRecoveryWarning(settingsLoad.WarningMessage);
            }

            if (!string.IsNullOrWhiteSpace(launchOptions.ProfileId))
            {
                window.Show();
                window.Hide();
                AppLog.Write("main-window hidden for profile launch");
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
        mouseOutputRouter?.Dispose();
        steamInputMouseEmitter?.Dispose();
        trayIconHost?.Dispose();
        shutdownSignalListener?.Dispose();
        rawMouseInputWindowHook?.Dispose();

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
        AppLog.Write("dispatcher-unhandled-exception");
        AppLog.WriteException("dispatcher-unhandled-exception", e.Exception);
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

    private static void ShowSettingsRecoveryWarning(string message)
    {
        AppLog.Write(message);
        _ = MessageBox.Show(
            message,
            "Steam HID Bridge Settings Reset",
            MessageBoxButton.OK,
            MessageBoxImage.Warning);
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
