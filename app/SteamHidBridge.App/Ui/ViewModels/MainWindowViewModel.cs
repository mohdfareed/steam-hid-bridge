namespace SteamHidBridge.App.Ui.ViewModels;

internal sealed class MainWindowViewModel(
    string activeProfileId,
    ProfileSettingsViewModel profile,
    GeneralSettingsViewModel general,
    OutputViewModel output)
{
    private readonly string activeProfileId = activeProfileId.Trim();

    public ProfileSettingsViewModel Profile { get; } = profile;
    public GeneralSettingsViewModel General { get; } = general;
    public OutputViewModel Output { get; } = output;

    public string ActiveProfileText => string.IsNullOrWhiteSpace(activeProfileId) ? "No active profile" : $"Active profile: {activeProfileId}";
    public string TrayText => string.IsNullOrWhiteSpace(activeProfileId) ? "No profile" : $"Profile: {activeProfileId}";
    public string ProcessText => $"{ActiveProfileText} - PID {System.Environment.ProcessId}";
    public string WindowTitle => string.IsNullOrWhiteSpace(activeProfileId)
        ? "Steam HID Bridge"
        : $"Steam HID Bridge - {activeProfileId}";
}
