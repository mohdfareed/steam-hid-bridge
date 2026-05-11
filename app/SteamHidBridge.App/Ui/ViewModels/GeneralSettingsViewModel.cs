using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Input;
using SteamHidBridge.App.Configuration;
using SteamHidBridge.App.Platform;
using SteamHidBridge.App.Platform.App;

namespace SteamHidBridge.App.Ui.ViewModels;

internal sealed class GeneralSettingsViewModel : ObservableObject
{
    private readonly BridgeAppService appService;
    private readonly Action<int?> applyBoardPort;
    private readonly Action<AppTheme> applyTheme;
    private readonly Func<AppUpdateCheckResult, bool> confirmUpdate;
    private string savedSrmManifestPath = string.Empty;
    private string savedBoardPort = string.Empty;
    private AppTheme savedTheme;
    private readonly AsyncRelayCommand saveGeneralCommand;

    public GeneralSettingsViewModel(
        BridgeAppService appService,
        Action<int?> applyBoardPort,
        Action<AppTheme> applyTheme,
        Func<AppUpdateCheckResult, bool> confirmUpdate)
    {
        this.appService = appService;
        this.applyBoardPort = applyBoardPort;
        this.applyTheme = applyTheme;
        this.confirmUpdate = confirmUpdate;

        saveGeneralCommand = new AsyncRelayCommand(SaveGeneralAsync, HasChanges);
        SaveGeneralCommand = saveGeneralCommand;
        ExportSrmManifestCommand = new AsyncRelayCommand(ExportSrmManifestAsync);
        UpdateFirmwareCommand = new AsyncRelayCommand(UpdateFirmwareAsync);
        CheckForUpdateCommand = new AsyncRelayCommand(CheckForUpdateAsync);
        OpenAppDataCommand = new AsyncRelayCommand(OpenAppDataAsync);

        SrmManifestPath = appService.SrmManifestPath;
        BoardPort = appService.BoardPort?.ToString() ?? string.Empty;
        SelectedTheme = appService.Theme;
        savedTheme = SelectedTheme;
        savedSrmManifestPath = SrmManifestPath.Trim();
        savedBoardPort = BoardPort.Trim();
    }

    public event Action<int>? ExitRequested;

    public System.Collections.ObjectModel.ObservableCollection<AppTheme> ThemeOptions { get; } = [AppTheme.System, AppTheme.Light, AppTheme.Dark];

    public ICommand SaveGeneralCommand { get; }
    public ICommand ExportSrmManifestCommand { get; }
    public ICommand UpdateFirmwareCommand { get; }
    public ICommand CheckForUpdateCommand { get; }
    public ICommand OpenAppDataCommand { get; }

    public static string AppDataPath => BridgeAppService.AppDataPath;
    public string VersionText => appService.VersionText;
    public string BoardFirmwareText => appService.HasBundledFirmware
        ? "Flash the packaged firmware to update the board."
        : "Firmware package is missing.";

    public string SrmManifestPath
    {
        get;
        set
        {
            if (SetProperty(ref field, value))
            {
                saveGeneralCommand.RaiseCanExecuteChanged();
            }
        }
    } = string.Empty;

    public string BoardPort
    {
        get;
        set
        {
            if (SetProperty(ref field, value))
            {
                saveGeneralCommand.RaiseCanExecuteChanged();
            }
        }
    } = string.Empty;

    public AppTheme SelectedTheme
    {
        get;
        set
        {
            if (SetProperty(ref field, value))
            {
                applyTheme(value);
                saveGeneralCommand.RaiseCanExecuteChanged();
            }
        }
    }

    private bool HasChanges()
    {
        return savedTheme != SelectedTheme
            || ParseBoardPort(savedBoardPort) != ParseBoardPort(BoardPort)
            || !string.Equals(savedSrmManifestPath, SrmManifestPath.Trim(), StringComparison.Ordinal);
    }

    private Task SaveGeneralAsync()
    {
        int? boardPortNumber = ParseBoardPort(BoardPort);
        if (!string.IsNullOrWhiteSpace(BoardPort) && boardPortNumber is null)
        {
            UserDialogs.ShowError("Board port must be a positive integer.");
            return Task.CompletedTask;
        }

        appService.SaveGeneral(SelectedTheme, boardPortNumber, SrmManifestPath);
        applyBoardPort(appService.BoardPort);
        savedTheme = appService.Theme;
        savedSrmManifestPath = SrmManifestPath.Trim();
        savedBoardPort = BoardPort.Trim();
        saveGeneralCommand.RaiseCanExecuteChanged();
        return Task.CompletedTask;
    }

    private Task ExportSrmManifestAsync()
    {
        try
        {
            appService.ExportSrmManifest();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or InvalidOperationException)
        {
            UserDialogs.ShowError($"Could not write Steam ROM Manager manifest: {ex.Message}");
        }

        return Task.CompletedTask;
    }

    private Task OpenAppDataAsync()
    {
        try
        {
            BridgeAppService.OpenAppData();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            UserDialogs.ShowError($"Could not open app data folder: {ex.Message}");
        }

        return Task.CompletedTask;
    }

    private async Task UpdateFirmwareAsync()
    {
        try
        {
            await appService.UpdateFirmwareAsync().ConfigureAwait(true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            UserDialogs.ShowError($"Board firmware update failed to start: {ex.Message}");
        }

        OnPropertyChanged(nameof(BoardFirmwareText));
    }

    private async Task CheckForUpdateAsync()
    {
        try
        {
            AppUpdateCheckResult update = await appService.CheckForUpdateAsync().ConfigureAwait(true);
            if (!update.IsUpdateAvailable)
            {
                UserDialogs.ShowInfo($"Already on the latest release ({update.CurrentVersionText}).");
                return;
            }

            if (!confirmUpdate(update))
            {
                return;
            }

            await BridgeAppService.StartUpdateAsync(update).ConfigureAwait(true);
            ExitRequested?.Invoke(0);
        }
        catch (Exception ex)
        {
            UserDialogs.ShowError($"Update check failed: {ex.Message}");
        }
    }

    private static int? ParseBoardPort(string value)
    {
        string trimmed = value.Trim();
        return string.IsNullOrWhiteSpace(trimmed)
            ? null
            : int.TryParse(trimmed, out int portNumber) && portNumber > 0
            ? portNumber
            : null;
    }
}
