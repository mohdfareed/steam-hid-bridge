using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using SteamHidBridge.App.Configuration;
using SteamHidBridge.App.Core;
using SteamHidBridge.App.Platform;
using SteamHidBridge.App.Platform.App;

namespace SteamHidBridge.App.Ui.ViewModels;

internal sealed class GeneralSettingsViewModel : ObservableObject
{
    private readonly BridgeAppService appService;
    private readonly Action<int?> applyBoardPort;
    private readonly Action<string, int> applyViiperEndpoint;
    private readonly Action<AppTheme> applyTheme;
    private readonly Func<AppUpdateCheckResult, bool> confirmUpdate;
    private AppTheme savedTheme;
    private string savedBoardPort = string.Empty;
    private string savedViiperHost = string.Empty;
    private string savedViiperPort = string.Empty;
    private string savedSrmManifestPath = string.Empty;
    private int viiperCheckVersion;

    public GeneralSettingsViewModel(
        BridgeAppService appService,
        Action<int?> applyBoardPort,
        Action<string, int> applyViiperEndpoint,
        Action<AppTheme> applyTheme,
        Func<AppUpdateCheckResult, bool> confirmUpdate)
    {
        this.appService = appService;
        this.applyBoardPort = applyBoardPort;
        this.applyViiperEndpoint = applyViiperEndpoint;
        this.applyTheme = applyTheme;
        this.confirmUpdate = confirmUpdate;

        SaveGeneralCommand = new AsyncRelayCommand(SaveGeneralAsync);
        ExportSrmManifestCommand = new AsyncRelayCommand(ExportSrmManifestAsync);
        UpdateFirmwareCommand = new AsyncRelayCommand(UpdateFirmwareAsync);
        CheckForUpdateCommand = new AsyncRelayCommand(CheckForUpdateAsync);
        OpenAppDataCommand = new AsyncRelayCommand(OpenAppDataAsync);

        SrmManifestPath = appService.SrmManifestPath;
        BoardPort = appService.BoardPort?.ToString() ?? string.Empty;
        ViiperHost = appService.ViiperHost;
        ViiperPort = appService.ViiperPort.ToString();
        SelectedTheme = appService.Theme;
        savedTheme = SelectedTheme;
        savedBoardPort = BoardPort.Trim();
        savedViiperHost = ViiperHost.Trim();
        savedViiperPort = ViiperPort.Trim();
        savedSrmManifestPath = SrmManifestPath.Trim();
        QueueViiperCheck();
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
        ? "Press program button on the board and click Flash to update firmware."
        : "Firmware package is missing.";
    public FontWeight SaveFontWeight => HasUnsavedChanges ? FontWeights.Bold : FontWeights.Normal;
    public static Brush SaveErrorBrush => Brushes.IndianRed;
    public Brush BoardStatusBrush
    {
        get;
        private set => SetProperty(ref field, value);
    } = Brushes.Gray;

    public string BoardStatusToolTip
    {
        get;
        private set => SetProperty(ref field, value);
    } = "Not connected.";

    public Brush ViiperStatusBrush
    {
        get;
        private set => SetProperty(ref field, value);
    } = Brushes.Gray;

    public string ViiperStatusToolTip
    {
        get;
        private set => SetProperty(ref field, value);
    } = "Not checked yet.";

    public string SaveErrorText
    {
        get;
        private set => SetProperty(ref field, value);
    } = string.Empty;

    public string SrmManifestPath
    {
        get;
        set
        {
            if (SetProperty(ref field, value))
            {
                OnEditChanged();
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
                OnEditChanged();
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
                OnEditChanged();
            }
        }
    }

    public string ViiperHost
    {
        get;
        set
        {
            if (SetProperty(ref field, value))
            {
                ResetViiperStatus();
                QueueViiperCheck();
                OnEditChanged();
            }
        }
    } = "localhost";

    public string ViiperPort
    {
        get;
        set
        {
            if (SetProperty(ref field, value))
            {
                ResetViiperStatus();
                QueueViiperCheck();
                OnEditChanged();
            }
        }
    } = "3242";

    public void ApplySessionStatus(BridgeSessionStatus status)
    {
        ApplyBoardStatus(status.BoardOutput);
        ApplyViiperStatus(status.ViiperOutput);
    }

    private Task SaveGeneralAsync()
    {
        if (!TryApplyGeneralSettings(out string? error))
        {
            SaveErrorText = error!;
            return Task.CompletedTask;
        }

        SaveErrorText = string.Empty;
        return Task.CompletedTask;
    }

    private Task ExportSrmManifestAsync()
    {
        try
        {
            if (!TryApplyGeneralSettings(out string? error))
            {
                SaveErrorText = error!;
                return Task.CompletedTask;
            }

            SaveErrorText = string.Empty;
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

    private void QueueViiperCheck()
    {
        int version = Interlocked.Increment(ref viiperCheckVersion);
        _ = CheckViiperAsync(version);
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

    private static int? ParseRequiredPort(string value)
    {
        string trimmed = value.Trim();
        return int.TryParse(trimmed, out int portNumber) && portNumber > 0
            ? portNumber
            : null;
    }

    private void ResetViiperStatus()
    {
        ViiperStatusBrush = Brushes.Gray;
        ViiperStatusToolTip = "Checking...";
    }

    private async Task CheckViiperAsync(int version)
    {
        string host = ViiperHost.Trim();
        int? port = ParseRequiredPort(ViiperPort);
        if (string.IsNullOrWhiteSpace(host) || port is null)
        {
            ViiperStatusBrush = Brushes.Gray;
            ViiperStatusToolTip = "Enter a valid host and port.";
            return;
        }

        try
        {
            await Task.Delay(300).ConfigureAwait(true);
            if (version != viiperCheckVersion)
            {
                return;
            }

            await BridgeAppService.CheckViiperAsync(host, port.Value).ConfigureAwait(true);
            if (version == viiperCheckVersion)
            {
                ViiperStatusBrush = Brushes.SeaGreen;
                ViiperStatusToolTip = $"Connected to {host}:{port.Value}.";
            }
        }
        catch (Exception ex)
        {
            if (version == viiperCheckVersion)
            {
                ViiperStatusBrush = Brushes.IndianRed;
                ViiperStatusToolTip = $"Could not reach {host}:{port.Value}. {ex.Message}";
            }
        }
    }

    private bool TryApplyGeneralSettings(out string? error)
    {
        int? boardPortNumber = ParseBoardPort(BoardPort);
        if (!string.IsNullOrWhiteSpace(BoardPort) && boardPortNumber is null)
        {
            error = "Board port must be a positive integer.";
            return false;
        }

        string viiperHost = ViiperHost.Trim();
        if (string.IsNullOrWhiteSpace(viiperHost))
        {
            error = "VIIPER host is required.";
            return false;
        }

        int? viiperPortNumber = ParseRequiredPort(ViiperPort);
        if (viiperPortNumber is null)
        {
            error = "VIIPER port must be a positive integer.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(SrmManifestPath))
        {
            error = "SRM manifest path is required.";
            return false;
        }

        appService.SaveGeneral(SelectedTheme, boardPortNumber, SrmManifestPath, viiperHost, viiperPortNumber.Value);
        applyBoardPort(appService.BoardPort);
        applyViiperEndpoint(appService.ViiperHost, appService.ViiperPort);
        savedTheme = SelectedTheme;
        savedBoardPort = BoardPort.Trim();
        savedViiperHost = ViiperHost.Trim();
        savedViiperPort = ViiperPort.Trim();
        savedSrmManifestPath = SrmManifestPath.Trim();
        OnPropertyChanged(nameof(SaveFontWeight));
        error = null;
        return true;
    }

    private void ApplyBoardStatus(OutputStatus status)
    {
        switch (status.State)
        {
            case OutputConnectionState.Connected:
                BoardStatusBrush = Brushes.SeaGreen;
                BoardStatusToolTip = string.IsNullOrWhiteSpace(status.Endpoint)
                    ? "Connected."
                    : $"Connected: {status.Endpoint}.";
                break;
            case OutputConnectionState.Error:
            case OutputConnectionState.Disconnected:
                BoardStatusBrush = Brushes.IndianRed;
                BoardStatusToolTip = string.IsNullOrWhiteSpace(status.Endpoint)
                    ? "Not connected."
                    : $"Not connected: {status.Endpoint}.";
                break;
            case OutputConnectionState.Idle:
                break;
            default:
                BoardStatusBrush = Brushes.Gray;
                BoardStatusToolTip = "Not connected.";
                break;
        }
    }

    private void ApplyViiperStatus(OutputStatus status)
    {
        switch (status.State)
        {
            case OutputConnectionState.Connected:
                ViiperStatusBrush = Brushes.SeaGreen;
                ViiperStatusToolTip = string.IsNullOrWhiteSpace(status.Endpoint)
                    ? "Connected."
                    : $"Connected: {status.Endpoint}.";
                break;
            case OutputConnectionState.Error:
            case OutputConnectionState.Disconnected:
                ViiperStatusBrush = Brushes.IndianRed;
                ViiperStatusToolTip = string.IsNullOrWhiteSpace(status.Endpoint)
                    ? "Not connected."
                    : $"Not connected: {status.Endpoint}.";
                break;
            case OutputConnectionState.Idle:
                break;
            default:
                break;
        }
    }

    private bool HasUnsavedChanges =>
        savedTheme != SelectedTheme
        || !string.Equals(savedBoardPort, BoardPort.Trim(), StringComparison.Ordinal)
        || !string.Equals(savedViiperHost, ViiperHost.Trim(), StringComparison.Ordinal)
        || !string.Equals(savedViiperPort, ViiperPort.Trim(), StringComparison.Ordinal)
        || !string.Equals(savedSrmManifestPath, SrmManifestPath.Trim(), StringComparison.Ordinal);

    private void OnEditChanged()
    {
        if (!string.IsNullOrEmpty(SaveErrorText))
        {
            SaveErrorText = string.Empty;
        }

        OnPropertyChanged(nameof(SaveFontWeight));
    }
}
