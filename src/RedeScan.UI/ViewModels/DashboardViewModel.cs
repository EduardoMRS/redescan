using System.Net;
using System.Threading;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RedeScan.Core.Models;
using RedeScan.Core.Services;
using RedeScan.UI.Services;

namespace RedeScan.UI.ViewModels;

/// <summary>
/// Painel inicial: resumo da rede local + atalhos para dispositivos com
/// interface web. Faz auto-descoberta periódica enquanto houver rede e, no
/// primeiro acesso, procura possíveis roteadores.
/// </summary>
public partial class DashboardViewModel : ViewModelBase, IDisposable
{
    private readonly INetworkInterfaceService _networkInterface;
    private readonly IWebDeviceScanner _webScanner;
    private readonly ISystemBrowserLauncher _systemBrowser;
    private readonly IServiceProvider _services;

    private Timer? _autoTimer;
    private CancellationTokenSource? _scanCts;
    private bool _firstRunDone;
    private string? _lastSubnet;

    public override string Title => "Painel";
    public override string Icon => "🏠";

    [ObservableProperty] private string _localIp = "—";
    [ObservableProperty] private string _gateway = "—";
    [ObservableProperty] private string _subnetPrefix = "—";
    [ObservableProperty] private string _hostName = Dns.GetHostName();

    [ObservableProperty] private bool _isDiscovering;
    [ObservableProperty] private string _discoveryStatus = "Aguardando rede…";
    [ObservableProperty] private bool _autoDiscoveryEnabled = true;

    public ObservableCollection<string> ActiveInterfaces { get; } = new();

    /// <summary>Dispositivos com interface web encontrados na rede (atalhos).</summary>
    public ObservableCollection<WebDevice> WebDevices { get; } = new();

    [ObservableProperty] private bool _hasNoWebDevices = true;

    public DashboardViewModel(
        INetworkInterfaceService networkInterface,
        IWebDeviceScanner webScanner,
        ISystemBrowserLauncher systemBrowser,
        IServiceProvider services)
    {
        _networkInterface = networkInterface;
        _webScanner = webScanner;
        _systemBrowser = systemBrowser;
        _services = services;

        WebDevices.CollectionChanged += (_, _) => HasNoWebDevices = WebDevices.Count == 0;

        Refresh();
        StartAutoDiscovery();
    }

    [RelayCommand]
    private void Refresh()
    {
        LocalIp = _networkInterface.GetPrimaryLocalIpAddress()?.ToString() ?? "indisponível";
        Gateway = _networkInterface.GetDefaultGateway()?.ToString() ?? "indisponível";
        SubnetPrefix = _networkInterface.GetSubnetPrefix() is { } prefix
            ? $"{prefix.TrimEnd('.')}.0/24"
            : "indisponível";

        ActiveInterfaces.Clear();
        foreach (var (name, ip, gw) in _networkInterface.GetAllActiveInterfaces())
        {
            ActiveInterfaces.Add($"{name}  —  {ip}" + (gw is not null ? $"  (gw {gw})" : string.Empty));
        }
    }

    /// <summary>
    /// Dispara a auto-descoberta: a cada ciclo verifica se há rede e, se a faixa
    /// mudou (ou é a primeira vez), varre os dispositivos web automaticamente.
    /// </summary>
    private void StartAutoDiscovery()
    {
        // Primeiro ciclo imediato; depois a cada 60s.
        _autoTimer = new Timer(_ => _ = AutoTickAsync(), null, TimeSpan.Zero, TimeSpan.FromSeconds(60));
    }

    private async Task AutoTickAsync()
    {
        if (!AutoDiscoveryEnabled || IsDiscovering)
        {
            return;
        }

        var prefix = _networkInterface.GetSubnetPrefix();
        if (string.IsNullOrWhiteSpace(prefix))
        {
            DiscoveryStatus = "Sem rede detectada.";
            return;
        }

        // No primeiro acesso, procura roteadores antes da varredura completa.
        if (!_firstRunDone)
        {
            _firstRunDone = true;
            await FindRoutersAsync();
        }

        // Só re-varre se a faixa mudou (trocou de Wi-Fi/cabo) ou a lista está vazia.
        if (prefix != _lastSubnet || WebDevices.Count == 0)
        {
            _lastSubnet = prefix;
            await DiscoverAsync();
        }
    }

    [RelayCommand]
    private async Task DiscoverAsync()
    {
        var prefix = _networkInterface.GetSubnetPrefix();
        if (string.IsNullOrWhiteSpace(prefix))
        {
            DiscoveryStatus = "Sem rede detectada.";
            return;
        }

        _scanCts?.Cancel();
        _scanCts = new CancellationTokenSource();
        IsDiscovering = true;
        DiscoveryStatus = $"Procurando dispositivos web em {prefix.TrimEnd('.')}.0/24…";

        var seen = new HashSet<string>(WebDevices.Select(d => d.IpAddress));
        var progress = new Progress<WebDevice>(dev =>
        {
            // Garante mutação da coleção na thread de UI (o scan roda em background).
            Dispatcher.UIThread.Post(() =>
            {
                if (seen.Add(dev.IpAddress))
                {
                    WebDevices.Add(dev);
                }
            });
        });

        try
        {
            await _webScanner.ScanAsync(prefix, progress, _scanCts.Token);
            DiscoveryStatus = $"{WebDevices.Count} dispositivo(s) com web encontrados.";
        }
        catch (OperationCanceledException)
        {
            DiscoveryStatus = "Descoberta cancelada.";
        }
        catch (Exception ex)
        {
            DiscoveryStatus = $"Erro na descoberta: {ex.Message}";
        }
        finally
        {
            IsDiscovering = false;
        }
    }

    [RelayCommand]
    private async Task FindRoutersAsync()
    {
        DiscoveryStatus = "Procurando possíveis roteadores…";
        try
        {
            var routers = await _webScanner.FindRoutersAsync();
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                var seen = new HashSet<string>(WebDevices.Select(d => d.IpAddress));
                foreach (var r in routers)
                {
                    if (seen.Add(r.IpAddress))
                    {
                        WebDevices.Insert(0, r);
                    }
                }
            });

            DiscoveryStatus = routers.Count > 0
                ? $"{routers.Count} roteador(es) encontrado(s)."
                : "Nenhum roteador óbvio encontrado.";
        }
        catch (Exception ex)
        {
            DiscoveryStatus = $"Erro ao procurar roteadores: {ex.Message}";
        }
    }

    [RelayCommand]
    private void OpenDeviceInternal(WebDevice? device)
    {
        if (device is null)
        {
            return;
        }

        var navigator = _services.GetService(typeof(IAppNavigator)) as IAppNavigator;
        navigator?.OpenInInternalBrowser(device.PreferredUrl);
    }

    [RelayCommand]
    private void OpenDeviceSystem(WebDevice? device)
    {
        if (device is not null)
        {
            _systemBrowser.Open(device.PreferredUrl);
        }
    }

    public void Dispose()
    {
        _autoTimer?.Dispose();
        _scanCts?.Cancel();
        _scanCts?.Dispose();
    }
}
