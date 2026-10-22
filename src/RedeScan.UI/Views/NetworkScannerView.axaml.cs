using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using RedeScan.UI.ViewModels;

namespace RedeScan.UI.Views;

public partial class NetworkScannerView : UserControl
{
    public NetworkScannerView() => InitializeComponent();

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    /// <summary>Duplo-clique em um host dispara o scan de portas rápido.</summary>
    private void OnHostDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (DataContext is NetworkScannerViewModel vm &&
            vm.SelectedHost is not null &&
            vm.ScanPortsOfCommand.CanExecute(vm.SelectedHost))
        {
            vm.ScanPortsOfCommand.Execute(vm.SelectedHost);
        }
    }
}
