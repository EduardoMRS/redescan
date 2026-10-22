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
        BrowserViewModel browser)
    {
        _browser = browser;

        Pages = new ObservableCollection<ViewModelBase>
        {
            dashboard,
            networkScanner,
            diagnostics,
            wifi,
            cameras,
            shortcuts,
            browser
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
}
