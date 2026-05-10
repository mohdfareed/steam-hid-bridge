namespace SteamHidBridge.App.Input;

public sealed record MouseInputStatistics(
    long EventCount,
    long PreviewCount,
    double EventsPerSecond,
    double PreviewFramesPerSecond);
