using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace RedeScan.UI.Views;

public partial class NetworkConfigView : UserControl
{
    public NetworkConfigView() => InitializeComponent();

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
}
