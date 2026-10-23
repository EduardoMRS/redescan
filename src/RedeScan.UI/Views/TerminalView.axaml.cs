using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using RedeScan.UI.ViewModels;

namespace RedeScan.UI.Views;

/// <summary>
/// Terminal com abas. O conteúdo de cada aba vem de um DataTemplate, então os
/// controles (caixa de comando e saída) são ligados quando entram na árvore
/// visual, usando o DataContext da própria aba.
/// </summary>
public partial class TerminalView : UserControl
{
    public TerminalView() => InitializeComponent();

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    /// <summary>Liga Enter/Tab/Ctrl+C/↑/↓ da caixa de comando (handler tunnel, antes do TextBox).</summary>
    private void OnCommandBoxAttached(object? sender, VisualTreeAttachmentEventArgs e)
    {
        if (sender is not TextBox box || box.Tag is "wired")
        {
            return;
        }

        box.Tag = "wired";
        box.AddHandler(KeyDownEvent, OnCommandKeyDown, RoutingStrategies.Tunnel);

        // Ao conectar, o foco vai para a caixa de comando.
        box.DataContextChanged += (_, _) => WatchConnect(box);
        WatchConnect(box);
    }

    private static void WatchConnect(TextBox box)
    {
        if (box.DataContext is TerminalTabViewModel tab)
        {
            tab.PropertyChanged += (_, args) =>
            {
                if (args.PropertyName == nameof(TerminalTabViewModel.IsConnected) && tab.IsConnected)
                {
                    Dispatcher.UIThread.Post(() => box.Focus());
                }
            };
        }
    }

    private void OnCommandKeyDown(object? sender, KeyEventArgs e)
    {
        if ((sender as TextBox)?.DataContext is not TerminalTabViewModel tab)
        {
            return;
        }

        if (e.Key == Key.Enter)
        {
            tab.SendCommand.Execute(null);
            e.Handled = true;
        }
        else if (e.Key == Key.C && e.KeyModifiers.HasFlag(KeyModifiers.Control) &&
                 string.IsNullOrEmpty((sender as TextBox)?.SelectedText))
        {
            tab.SendKeyCommand.Execute("ctrl-c");
            e.Handled = true;
        }
        else if (e.Key == Key.Tab)
        {
            tab.SendKeyCommand.Execute("tab");
            e.Handled = true;
        }
        else if (e.Key == Key.Up)
        {
            tab.HistoryPrevious();
            e.Handled = true;
        }
        else if (e.Key == Key.Down)
        {
            tab.HistoryNext();
            e.Handled = true;
        }
    }

    private void OnPasswordKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && (sender as Control)?.DataContext is TerminalTabViewModel tab &&
            tab.ConnectCommand.CanExecute(null))
        {
            tab.ConnectCommand.Execute(null);
            e.Handled = true;
        }
    }

    /// <summary>Rola a saída para o fim a cada texto novo (como um terminal real).</summary>
    private void OnOutputAttached(object? sender, VisualTreeAttachmentEventArgs e)
    {
        if (sender is not ScrollViewer scroll || scroll.Tag is "wired")
        {
            return;
        }

        scroll.Tag = "wired";
        TerminalTabViewModel? current = null;

        void OnChanged(object? s, PropertyChangedEventArgs args)
        {
            if (args.PropertyName == nameof(TerminalTabViewModel.Output))
            {
                Dispatcher.UIThread.Post(scroll.ScrollToEnd, DispatcherPriority.Background);
            }
        }

        void Rebind()
        {
            if (current is not null)
            {
                current.PropertyChanged -= OnChanged;
            }

            current = scroll.DataContext as TerminalTabViewModel;
            if (current is not null)
            {
                current.PropertyChanged += OnChanged;
                Dispatcher.UIThread.Post(scroll.ScrollToEnd, DispatcherPriority.Background);
            }
        }

        scroll.DataContextChanged += (_, _) => Rebind();
        Rebind();
    }
}
