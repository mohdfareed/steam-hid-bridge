using System;
using System.Threading.Tasks;
using SteamHidBridge.App.Platform;

namespace SteamHidBridge.App.Ui.ViewModels;

internal sealed partial class MainWindowViewModel
{
    private async Task CheckForUpdateAsync()
    {
        try
        {
            AppUpdateCheckResult update = await appUpdater.CheckLatestAsync();
            if (!update.IsUpdateAvailable)
            {
                UserDialogs.ShowInfo($"Already on the latest release ({update.CurrentVersionText}).");
                return;
            }

            if (!confirmUpdate(update))
            {
                return;
            }

            AppUpdater.StartUpdate(update);
            RequestExit(0);
        }
        catch (Exception ex)
        {
            UserDialogs.ShowError($"Update check failed: {ex.Message}");
        }
    }
}
