using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace RedeScan.UI.Views;

public partial class DiagnosticsView : UserControl
{
    public DiagnosticsView() => InitializeComponent();

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
}
