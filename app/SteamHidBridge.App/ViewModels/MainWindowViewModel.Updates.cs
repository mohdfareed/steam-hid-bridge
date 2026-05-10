using System;
using System.Threading.Tasks;
using SteamHidBridge.App.Updates;

namespace SteamHidBridge.App.ViewModels;

public sealed partial class MainWindowViewModel
{
    private async Task CheckForUpdateAsync()
    {
        try
        {
            SetActivity("Checking for updates...");

            var update = await appUpdater.CheckLatestAsync();
            if (!update.IsUpdateAvailable)
            {
                SetActivity($"Already on the latest release ({update.CurrentVersionText}).");
                return;
            }

            if (!confirmUpdate(update))
            {
                SetActivity($"Update available: {update.LatestVersionText}.");
                return;
            }

            AppUpdater.StartUpdate(update);
            SetActivity($"Starting update to {update.LatestVersionText}.");
            RequestExit(0);
        }
        catch (Exception ex)
        {
            SetError($"Update check failed: {ex.Message}");
        }
    }
}
