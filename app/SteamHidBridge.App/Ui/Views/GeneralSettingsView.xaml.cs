using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace SteamHidBridge.App.Ui.Views;

public partial class GeneralSettingsView : UserControl
{
    public GeneralSettingsView()
    {
        InitializeComponent();
    }

    private void BoardPort_PreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        e.Handled = !e.Text.All(char.IsDigit);
    }

    private void BoardPort_Pasting(object sender, DataObjectPastingEventArgs e)
    {
        if (!e.DataObject.GetDataPresent(DataFormats.Text))
        {
            e.CancelCommand();
            return;
        }

        if (e.DataObject.GetData(DataFormats.Text) is not string text || !text.All(char.IsDigit))
        {
            e.CancelCommand();
        }
    }
}
