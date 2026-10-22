using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using RedeScan.UI.ViewModels;

namespace RedeScan.UI.Views;

public partial class TerminalView : UserControl
{
    public TerminalView() => InitializeComponent();

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    // Enter envia o comando.
    private void OnCommandKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && DataContext is TerminalViewModel vm && vm.SendCommand.CanExecute(null))
        {
            vm.SendCommand.Execute(null);
            e.Handled = true;
        }
    }
}
