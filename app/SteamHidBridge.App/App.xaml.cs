using System.Windows;
using SteamHidBridge.App.Input;
using SteamHidBridge.App.Profiles;
using SteamHidBridge.App.Startup;
using SteamHidBridge.App.Transport;
using SteamHidBridge.App.ViewModels;
using SteamHidBridge.App.Views;
using SteamHidBridge.App.Windows;

[assembly: ThemeInfo(ResourceDictionaryLocation.None, ResourceDictionaryLocation.SourceAssembly)]

namespace SteamHidBridge.App;

public partial class App : Application
{
    private SingleInstanceGuard? singleInstance;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        BridgeLaunchOptions launchOptions = BridgeLaunchOptions.Parse(e.Args);
        singleInstance = SingleInstanceGuard.TryAcquire();
        if (singleInstance is null)
        {
            MessageBox.Show(
                "Steam HID Bridge is already running. Close the running instance before launching another profile.",
                "Steam HID Bridge",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            Shutdown(1);
            return;
        }

        BridgeProfileStore profileStore = BridgeProfileStore.LoadDefault();
        BridgeProfile profile = profileStore.Resolve(launchOptions.ProfileId);

        var window = new MainWindow
        {
            DataContext = new MainWindowViewModel(
                launchOptions,
                profile,
                new LoopbackBridgeTransport(),
                new SteamInputStatusSource(),
                new Win32ForegroundWindowService())
        };

        MainWindow = window;
        window.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        singleInstance?.Dispose();
        base.OnExit(e);
    }
}
