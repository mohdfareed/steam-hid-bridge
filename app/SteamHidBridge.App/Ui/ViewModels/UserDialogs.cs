using System.Windows;

namespace SteamHidBridge.App.Ui.ViewModels;

internal static class UserDialogs
{
    public static void ShowError(string message)
    {
        _ = MessageBox.Show(message, "Steam HID Bridge", MessageBoxButton.OK, MessageBoxImage.Error);
    }

    public static void ShowInfo(string message)
    {
        _ = MessageBox.Show(message, "Steam HID Bridge", MessageBoxButton.OK, MessageBoxImage.Information);
    }
}
