using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using RedeScan.UI.ViewModels;

namespace RedeScan.UI.Views;

public partial class TerminalView : UserControl
{
    private TerminalViewModel? _vm;

    public TerminalView()
    {
        InitializeComponent();

        // Tunnel: recebe o Enter ANTES do TextBox, que de outra forma pode
        // consumir a tecla sem que o comando seja enviado.
        var box = this.FindControl<TextBox>("CommandBox")!;
        box.AddHandler(KeyDownEvent, OnCommandKeyDown, RoutingStrategies.Tunnel);

        DataContextChanged += (_, _) =>
        {
            if (_vm is not null)
            {
                _vm.PropertyChanged -= OnVmPropertyChanged;
            }

            _vm = DataContext as TerminalViewModel;
            if (_vm is not null)
            {
                _vm.PropertyChanged += OnVmPropertyChanged;
            }
        };
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    private void OnCommandKeyDown(object? sender, KeyEventArgs e)
    {
        if (_vm is null)
        {
            return;
        }

        if (e.Key == Key.Enter)
        {
            _vm.SendCommand.Execute(null);
            e.Handled = true;
        }
        else if (e.Key == Key.C && e.KeyModifiers.HasFlag(KeyModifiers.Control) &&
                 string.IsNullOrEmpty((sender as TextBox)?.SelectedText))
        {
            _vm.SendKeyCommand.Execute("ctrl-c");
            e.Handled = true;
        }
        else if (e.Key == Key.Tab)
        {
            _vm.SendKeyCommand.Execute("tab");
            e.Handled = true;
        }
        else if (e.Key == Key.Up)
        {
            _vm.HistoryPrevious();
            e.Handled = true;
        }
        else if (e.Key == Key.Down)
        {
            _vm.HistoryNext();
            e.Handled = true;
        }
    }

    private void OnPasswordKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && _vm is not null && _vm.ConnectCommand.CanExecute(null))
        {
            _vm.ConnectCommand.Execute(null);
            e.Handled = true;
        }
    }

    private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(TerminalViewModel.Output))
        {
            // Rola para o fim a cada saída nova (como um terminal real).
            Dispatcher.UIThread.Post(() => this.FindControl<ScrollViewer>("OutputScroll")?.ScrollToEnd(),
                DispatcherPriority.Background);
        }
        else if (e.PropertyName == nameof(TerminalViewModel.IsConnected) && _vm?.IsConnected == true)
        {
            Dispatcher.UIThread.Post(() => this.FindControl<TextBox>("CommandBox")?.Focus());
        }
    }
}
