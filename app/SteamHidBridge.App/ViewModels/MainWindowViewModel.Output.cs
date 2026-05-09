using System.Threading;
using System.Threading.Tasks;
using SteamHidBridge.App.Transport;
using SteamHidBridge.Protocol;

namespace SteamHidBridge.App.ViewModels;

public sealed partial class MainWindowViewModel
{
    private async Task HandleOutputAsync(HidInputReport report, string source)
    {
        SetLastReport(report);
        framesPreviewed++;
        FramesPreviewedText = framesPreviewed.ToString();

        if (!isForwardingActive)
        {
            framesBlocked++;
            FramesBlockedText = framesBlocked.ToString();
            AddLog($"Previewed {source}; receiver is not active.");
            return;
        }

        byte[] payload = new byte[HidInputReport.WireSize];
        report.WriteTo(payload);
        BridgeTransportResult result = await transport.SendAsync(new BridgeFrame(BridgeCommand.HidInput, sequence++, payload), CancellationToken.None);

        if (result.Accepted)
        {
            framesForwarded++;
            FramesForwardedText = framesForwarded.ToString();
        }
        else
        {
            framesBlocked++;
            FramesBlockedText = framesBlocked.ToString();
        }

        AddLog($"{(result.Accepted ? "Forwarded" : "Rejected")} {source}: {result.Detail}");
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
