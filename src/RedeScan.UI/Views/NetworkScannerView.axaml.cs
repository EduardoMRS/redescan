using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace RedeScan.UI.Views;

public partial class NetworkScannerView : UserControl
{
    public NetworkScannerView() => InitializeComponent();

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
}
