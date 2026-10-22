using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RedeScan.Core.Models;
using RedeScan.Core.Services;
using RedeScan.UI.Services;

namespace RedeScan.UI.ViewModels;

/// <summary>
/// ViewModel raiz da janela principal. Mantém a lista de módulos exibidos na
/// barra lateral, o módulo atualmente ativo e o estado do tema (claro/escuro).
/// Implementa <see cref="IAppNavigator"/> (navegação a partir de qualquer
/// módulo) e <see cref="IShellPreferences"/> (integração com Configurações).
/// </summary>
public partial class MainWindowViewModel : ViewModelBase, IAppNavigator, IShellPreferences
{
    private readonly BrowserViewModel _browser;
    private readonly TerminalViewModel _terminal;
    private readonly INetworkInterfaceService _networkInterface;
    private readonly IConfigurationService _config;

    /// <summary>Todos os módulos, na ordem canônica (antes de filtrar).</summary>
    private readonly List<ViewModelBase> _allPages;

    public override string Title => "RedeScan";

    /// <summary>Módulos efetivamente exibidos no menu (após o filtro de visíveis).</summary>
    public ObservableCollection<ViewModelBase> Pages { get; } = new();

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
        TftpViewModel tftp,
        SettingsViewModel settings,
        INetworkInterfaceService networkInterface,
        IConfigurationService config)
    {
        _browser = browser;
        _terminal = terminal;
        _networkInterface = networkInterface;
        _config = config;

        _allPages = new List<ViewModelBase>
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
            tftp,
            settings
        };

        foreach (var page in _allPages)
        {
            Pages.Add(page);
        }

        _currentPage = dashboard;

        // Carrega as preferências salvas e aplica (tela inicial, tema, módulos).
        var saved = _config.LoadSettingsAsync().GetAwaiter().GetResult();
        ApplyPreferences(saved);
        ApplyBrowserHome(saved);
        var startup = _allPages.FirstOrDefault(p => p.Key == saved.StartupPageKey);
        if (startup is not null && Pages.Contains(startup))
        {
            _currentPage = startup;
        }
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

    /// <summary>Define o módulo clicado (botão direito) como tela inicial e persiste.</summary>
    [RelayCommand]
    private void SetAsStartup(ViewModelBase? page)
    {
        if (page is not null)
        {
            SetStartupModule(page.Key);
        }
    }

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

    // --------------------------------------------------- IShellPreferences
    public IReadOnlyList<(string Key, string Title, string Icon)> GetModules() =>
        _allPages.Select(p => (p.Key, p.Title, p.Icon)).ToList();

    public void ApplyPreferences(AppSettings settings)
    {
        SetTheme(settings.Theme);

        var enabled = settings.EnabledModules;
        var previous = CurrentPage;

        Pages.Clear();
        foreach (var page in _allPages)
        {
            // "settings" nunca é ocultado; demais seguem a lista (vazia = todos).
            if (page.Key == "settings" || enabled.Count == 0 || enabled.Contains(page.Key))
            {
                Pages.Add(page);
            }
        }

        // Se a página atual foi ocultada, cai para a primeira visível.
        CurrentPage = Pages.Contains(previous) ? previous : Pages.FirstOrDefault() ?? previous;
    }

    public void SetTheme(string theme)
    {
        IsDarkTheme = !string.Equals(theme, "Light", StringComparison.OrdinalIgnoreCase);
    }

    public void SetStartupModule(string moduleKey)
    {
        var settings = _config.LoadSettingsAsync().GetAwaiter().GetResult();
        settings.StartupPageKey = moduleKey;
        _config.SaveSettingsAsync(settings).GetAwaiter().GetResult();
    }

    /// <summary>Define a página inicial do navegador embutido conforme as prefs.</summary>
    private void ApplyBrowserHome(AppSettings settings)
    {
        var home = settings.BrowserHomeUrl;
        if (string.IsNullOrWhiteSpace(home))
        {
            return;
        }

        if (string.Equals(home, "router", StringComparison.OrdinalIgnoreCase))
        {
            var gw = _networkInterface.GetDefaultGateway()?.ToString();
            home = string.IsNullOrWhiteSpace(gw) ? "https://fast.com" : $"http://{gw}";
        }

        _browser.OpenInInternalBrowser(home);
    }
}
