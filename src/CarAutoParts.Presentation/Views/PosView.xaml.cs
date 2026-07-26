using System.Windows.Controls;
using System.Windows.Input;
using CarAutoParts.Presentation.ViewModels;

namespace CarAutoParts.Presentation.Views;

public partial class PosView : UserControl
{
    public PosView() => InitializeComponent();

    private async void BarcodeInput_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || DataContext is not PosViewModel vm)
            return;

        e.Handled = true;
        await vm.ProcessBarcodeScanCommand.ExecuteAsync(null);
        BarcodeInputBox.Focus();
    }
}
