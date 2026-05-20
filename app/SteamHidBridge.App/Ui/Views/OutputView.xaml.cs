using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using SteamHidBridge.App.Ui.ViewModels;

namespace SteamHidBridge.App.Ui.Views;

/// <summary>
/// Displays the current input preview and output state.
/// </summary>
public partial class OutputView : UserControl
{
    /// <summary>
    /// Initializes the output view.
    /// </summary>
    public OutputView()
    {
        InitializeComponent();
    }

    private void MoveBoard_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is not OutputViewModel viewModel || sender is not ButtonBase { Tag: string tag })
        {
            return;
        }

        string[] parts = tag.Split(',');
        if (parts.Length != 2
            || !short.TryParse(parts[0], out short deltaX)
            || !short.TryParse(parts[1], out short deltaY))
        {
            return;
        }

        viewModel.MoveBoard(deltaX, deltaY);
    }

    private void WheelBoard_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is not OutputViewModel viewModel || sender is not ButtonBase { Tag: string tag } || !sbyte.TryParse(tag, out sbyte wheel))
        {
            return;
        }

        viewModel.WheelBoard(wheel);
    }

    private void ReleaseButtons_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is OutputViewModel viewModel)
        {
            viewModel.ClearManualButtons();
        }
    }

    private void PinNextInput_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is OutputViewModel viewModel)
        {
            viewModel.RequestPinNextInput();
        }
    }

    private void ClearInputPin_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is OutputViewModel viewModel)
        {
            viewModel.ClearInputPin();
        }
    }
}
