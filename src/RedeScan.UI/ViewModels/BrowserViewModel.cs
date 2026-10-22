using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RedeScan.UI.Services;

namespace RedeScan.UI.ViewModels;

/// <summary>
/// Módulo 5 (navegador integrado). Mantém a URL atual e os comandos de
/// navegação. O render real do conteúdo web é feito pela View: quando a
/// aplicação é compilada com suporte a mídia nativa (CEF), usa um WebView
/// embutido; caso contrário, oferece abrir no navegador do sistema.
/// </summary>
public partial class BrowserViewModel : ViewModelBase, IAppNavigator
{
    private readonly ISystemBrowserLauncher _systemBrowser;

    public override string Title => "Navegador";
    public override string Icon => "🌐";

    /// <summary>URL atualmente exibida/solicitada (two-way com a barra de endereço).</summary>
    [ObservableProperty]
    private string _address = "https://fast.com";

    /// <summary>URL efetivamente carregada no WebView (dispara a navegação na View).</summary>
    [ObservableProperty]
    private string _currentUrl = "https://fast.com";

    /// <summary>Indica se o WebView nativo está disponível nesta build/plataforma.</summary>
    [ObservableProperty]
    private bool _isWebViewAvailable;

    public BrowserViewModel(ISystemBrowserLauncher systemBrowser)
    {
        _systemBrowser = systemBrowser;
    }

    [RelayCommand]
    private void Navigate()
    {
        if (string.IsNullOrWhiteSpace(Address))
        {
            return;
        }

        CurrentUrl = SystemBrowserLauncher.NormalizeUrl(Address.Trim());
    }

    [RelayCommand]
    private void OpenInSystemBrowser() => _systemBrowser.Open(Address);

    /// <summary>Navega para a URL informada e atualiza a barra de endereço.</summary>
    public void OpenInInternalBrowser(string url)
    {
        var normalized = SystemBrowserLauncher.NormalizeUrl(url);
        Address = normalized;
        CurrentUrl = normalized;
    }
}
