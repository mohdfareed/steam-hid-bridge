using SteamHidBridge.App.Input;
using SteamHidBridge.Protocol;

namespace SteamHidBridge.App.ViewModels;

public sealed partial class MainWindowViewModel
{
    private void PreviewMouseInput(MouseInputFrame frame)
    {
        SetLastReport(new HidInputReport(
            frame.PointerDeltaX,
            frame.PointerDeltaY,
            frame.VerticalWheel,
            frame.Buttons,
            KeyboardModifiers.None,
            0));
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
