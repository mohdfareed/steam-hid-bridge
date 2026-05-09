using System;
using System.Threading.Tasks;
using System.Windows;
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

            var result = MessageBox.Show(
                $"Install {update.LatestVersionText}?\n\nThis will close every Steam HID Bridge instance and any game processes launched by them.",
                "Steam HID Bridge Update",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes)
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
            SetActivity($"Update check failed: {ex.Message}");
        }
    }
}
