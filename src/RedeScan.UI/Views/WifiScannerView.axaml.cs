using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace RedeScan.UI.Views;

public partial class WifiScannerView : UserControl
{
    public WifiScannerView() => InitializeComponent();

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
}
