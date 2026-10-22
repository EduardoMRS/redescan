using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RedeScan.UI.Services;

namespace RedeScan.UI.ViewModels;

/// <summary>
/// ViewModel raiz da janela principal. Mantém a lista de módulos exibidos na
/// barra lateral, o módulo atualmente ativo e o estado do tema (claro/escuro).
/// Implementa <see cref="IAppNavigator"/> para que qualquer módulo possa pedir
/// a abertura de uma URL no navegador integrado.
/// </summary>
public partial class MainWindowViewModel : ViewModelBase, IAppNavigator
{
    private readonly BrowserViewModel _browser;
    private readonly TerminalViewModel _terminal;

    public override string Title => "RedeScan";

    public ObservableCollection<ViewModelBase> Pages { get; }

    [ObservableProperty]
    private ViewModelBase _currentPage;

    [ObservableProperty]
    private bool _isDarkTheme = true;

    public MainWindowViewModel(
        DashboardViewModel dashboard,
        NetworkScannerViewModel networkScanner,
        DiagnosticsViewModel diagnostics,
        WifiScannerViewModel wifi,
        CamerasViewModel cameras,
        ShortcutsViewModel shortcuts,
        BrowserViewModel browser,
        NetworkConfigViewModel networkConfig,
        TerminalViewModel terminal,
        TftpViewModel tftp)
    {
        _browser = browser;
        _terminal = terminal;

        Pages = new ObservableCollection<ViewModelBase>
        {
            dashboard,
            networkScanner,
            diagnostics,
            wifi,
            cameras,
            shortcuts,
            browser,
            networkConfig,
            terminal,
            tftp
        };

        _currentPage = dashboard;
    }

    [RelayCommand]
    private void Navigate(ViewModelBase? page)
    {
        if (page is not null)
        {
            CurrentPage = page;
        }
    }

    [RelayCommand]
    private void ToggleTheme() => IsDarkTheme = !IsDarkTheme;

    /// <summary>Abre a URL no navegador integrado e traz o módulo para frente.</summary>
    public void OpenInInternalBrowser(string url)
    {
        _browser.OpenInInternalBrowser(url);
        CurrentPage = _browser;
    }

    /// <summary>Abre o terminal SSH embutido já apontando para o host.</summary>
    public void OpenTerminal(string host, int port = 22, string? username = null)
    {
        _terminal.Prepare(host, port, username);
        CurrentPage = _terminal;
    }
}
