namespace SteamHidBridge.App.Windows;

public interface IForegroundWindowService
{
    ForegroundWindowSnapshot GetForegroundWindow();
}
