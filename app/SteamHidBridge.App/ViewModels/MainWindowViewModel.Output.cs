using SteamHidBridge.Protocol;

namespace SteamHidBridge.App.ViewModels;

public sealed partial class MainWindowViewModel
{
    private void PreviewOutput(HidInputReport report)
    {
        SetLastReport(report);
    }

    private void SetLastReport(HidInputReport report)
    {
        lastReport = report;
        LastOutputText = FormatReport(report);
        OnPropertyChanged(nameof(PointerText));
        OnPropertyChanged(nameof(WheelText));
        OnPropertyChanged(nameof(MouseButtonsText));
        OnPropertyChanged(nameof(KeyboardText));
        OnPropertyChanged(nameof(MouseLeftBrush));
        OnPropertyChanged(nameof(MouseRightBrush));
        OnPropertyChanged(nameof(MouseMiddleBrush));
        OnPropertyChanged(nameof(MouseBackBrush));
        OnPropertyChanged(nameof(MouseForwardBrush));
    }

    private static string FormatReport(HidInputReport report)
    {
        return $"mouse dx={report.PointerDeltaX}, dy={report.PointerDeltaY}, wheel={report.VerticalWheel}, buttons={report.MouseButtons}; keyboard modifiers={report.KeyboardModifiers}, usage=0x{report.KeyboardUsageId:X2}";
    }
}
