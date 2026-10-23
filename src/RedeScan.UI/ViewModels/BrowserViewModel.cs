using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RedeScan.Core.Models;
using RedeScan.Core.Services;
using RedeScan.UI.Services;

namespace RedeScan.UI.ViewModels;

/// <summary>Ações de navegação que a View executa no motor web.</summary>
public enum BrowserAction
{
    Back,
    Forward,
    Reload,
    Recreate,
    ShowWindow
}

/// <summary>
/// Navegador integrado. Usa o motor web do próprio sistema (WebView2 no
/// Windows, WebKitGTK no Linux, WebView no Android) — sem embutir Chromium.
/// A barra de endereço permite escolher rapidamente um dos roteadores/
/// dispositivos web descobertos na rede (via <see cref="IWebDeviceRegistry"/>).
/// </summary>
public partial class BrowserViewModel : ViewModelBase
{
    private readonly ISystemBrowserLauncher _systemBrowser;
    private readonly IConfigurationService _config;

    public override string Title => "Navegador";
    public override string Icon => "🌐";

    /// <summary>URL exibida na barra de endereço (two-way).</summary>
    [ObservableProperty]
    private string _address = "https://fast.com";

    /// <summary>Última URL pedida ao motor web (a View navega para ela ao ser criada).</summary>
    public string CurrentUrl { get; private set; } = "https://fast.com";

    /// <summary>True quando o navegador embutido está funcionando.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsWebViewUnavailable))]
    private bool _isWebViewAvailable;

    /// <summary>Motivo pelo qual o motor web não está disponível (para o fallback).</summary>
    [ObservableProperty] private string _unavailableReason = string.Empty;

    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private string _status = string.Empty;

    /// <summary>Mostra o aviso de certificado quando a página HTTPS foi recusada.</summary>
    [ObservableProperty] private bool _showCertificateHint;

    /// <summary>Aceitar certificados autoassinados (roteadores). Persistido nas preferências.</summary>
    [ObservableProperty] private bool _ignoreCertificateErrors;

    public bool IsWebViewUnavailable => !IsWebViewAvailable;

    /// <summary>
    /// True quando o motor do sistema não suporta ser embutido (WebKitGTK no
    /// Linux) e a página é exibida numa janela própria, acoplada sobre esta área.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowWindowHint))]
    private bool _isWindowMode;

    /// <summary>A janela do navegador está aberta (modo janela).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowWindowHint))]
    private bool _isWindowOpen;

    /// <summary>Mostra o aviso "janela fechada — reabrir" no modo janela.</summary>
    public bool ShowWindowHint => IsWindowMode && !IsWindowOpen;

    [RelayCommand]
    private void ShowWindow() => ActionRequested?.Invoke(BrowserAction.ShowWindow);

    /// <summary>Roteadores/dispositivos web descobertos na rede, para o seletor.</summary>
    public ReadOnlyObservableCollection<WebDevice> Routers { get; }

    /// <summary>Dispositivo escolhido no seletor — navega para ele ao mudar.</summary>
    [ObservableProperty]
    private WebDevice? _selectedRouter;

    /// <summary>Pedido de ação no motor web (voltar, avançar, recarregar, recriar).</summary>
    public event Action<BrowserAction>? ActionRequested;

    /// <summary>Pedido de navegação para uma URL.</summary>
    public event Action<string>? NavigateRequested;

    public BrowserViewModel(
        ISystemBrowserLauncher systemBrowser,
        IWebDeviceRegistry webRegistry,
        IConfigurationService config)
    {
        _systemBrowser = systemBrowser;
        _config = config;
        Routers = webRegistry.Devices;
        _ignoreCertificateErrors = config.LoadSettings().BrowserIgnoreCertificateErrors;
    }

    partial void OnSelectedRouterChanged(WebDevice? value)
    {
        if (value is not null)
        {
            OpenInInternalBrowser(value.PreferredUrl);
        }
    }

    partial void OnIgnoreCertificateErrorsChanged(bool value)
    {
        var settings = _config.LoadSettings();
        settings.BrowserIgnoreCertificateErrors = value;
        _ = _config.SaveSettingsAsync(settings);
        ShowCertificateHint = false;
        // A política de certificado só vale para um motor recém-criado.
        ActionRequested?.Invoke(BrowserAction.Recreate);
    }

    [RelayCommand]
    private void Navigate()
    {
        if (string.IsNullOrWhiteSpace(Address))
        {
            return;
        }

        OpenInInternalBrowser(Address.Trim());
    }

    [RelayCommand] private void Back() => ActionRequested?.Invoke(BrowserAction.Back);
    [RelayCommand] private void Forward() => ActionRequested?.Invoke(BrowserAction.Forward);
    [RelayCommand] private void Reload() => ActionRequested?.Invoke(BrowserAction.Reload);

    [RelayCommand]
    private void OpenInSystemBrowser() => _systemBrowser.Open(Address);

    /// <summary>Tenta a mesma página por HTTP (roteadores costumam aceitar os dois).</summary>
    [RelayCommand]
    private void TryHttp()
    {
        if (Address.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            OpenInInternalBrowser("http://" + Address["https://".Length..]);
        }
    }

    [RelayCommand]
    private void EnableIgnoreCertificates() => IgnoreCertificateErrors = true;

    /// <summary>Navega para a URL informada e atualiza a barra de endereço.</summary>
    public void OpenInInternalBrowser(string url)
    {
        var normalized = SystemBrowserLauncher.NormalizeUrl(url);
        Address = normalized;
        CurrentUrl = normalized;
        ShowCertificateHint = false;
        NavigateRequested?.Invoke(normalized);
    }

    /// <summary>Chamado pela View quando o motor reporta a URL real (redirecionamentos, links).</summary>
    public void ReportNavigated(string url)
    {
        Address = url;
        CurrentUrl = url;
    }
}
