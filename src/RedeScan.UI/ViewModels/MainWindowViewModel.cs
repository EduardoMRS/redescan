using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace RedeScan.UI.ViewModels;

/// <summary>
/// ViewModel raiz da janela principal. Mantém a lista de módulos exibidos na
/// barra lateral, o módulo atualmente ativo e o estado do tema (claro/escuro).
/// </summary>
public partial class MainWindowViewModel : ViewModelBase
{
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
        ShortcutsViewModel shortcuts)
    {
        Pages = new ObservableCollection<ViewModelBase>
        {
            dashboard,
            networkScanner,
            diagnostics,
            wifi,
            cameras,
            shortcuts
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
}
