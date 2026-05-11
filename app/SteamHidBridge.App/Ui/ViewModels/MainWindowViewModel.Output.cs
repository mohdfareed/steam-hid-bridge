using SteamHidBridge.App.Core;
using SteamHidBridge.Protocol;

namespace SteamHidBridge.App.Ui.ViewModels;

internal sealed partial class MainWindowViewModel
{
    private void PreviewMouseInput(MouseInputFrame frame)
    {
        SetLastReport(new HidInputReport(
            frame.PointerDeltaX,
            frame.PointerDeltaY,
            frame.VerticalWheel,
            frame.Buttons));
    }

    private void SetLastReport(HidInputReport report)
    {
        lastReport = report;
        LastOutputText = FormatReport(report);
        OnPropertyChanged(nameof(PointerText));
        OnPropertyChanged(nameof(WheelText));
        OnPropertyChanged(nameof(MouseButtonsText));
        OnPropertyChanged(nameof(MouseLeftBrush));
        OnPropertyChanged(nameof(MouseRightBrush));
        OnPropertyChanged(nameof(MouseMiddleBrush));
        OnPropertyChanged(nameof(MouseBackBrush));
        OnPropertyChanged(nameof(MouseForwardBrush));
    }

    private static string FormatReport(HidInputReport report)
    {
        return $"mouse dx={report.PointerDeltaX}, dy={report.PointerDeltaY}, wheel={report.VerticalWheel}, buttons={report.MouseButtons}";
    }
}
