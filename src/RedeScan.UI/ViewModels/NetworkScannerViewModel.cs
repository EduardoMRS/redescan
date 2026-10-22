using System.Threading;
using Avalonia;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RedeScan.Core.Models;
using RedeScan.Core.Services;
using RedeScan.UI.Services;

namespace RedeScan.UI.ViewModels;

/// <summary>
/// Módulo 2 — varredura da sub-rede (ping sweep) e scan de portas TCP do host
/// selecionado. Resultados chegam ao vivo pelos callbacks <see cref="IProgress{T}"/>.
/// </summary>
public partial class NetworkScannerViewModel : ViewModelBase
{
    private readonly INetworkScannerService _scanner;
    private readonly IPortScannerService _portScanner;
    private readonly INetworkInterfaceService _networkInterface;
    private readonly IPingService _pingService;
    private readonly IHostInspector _inspector;
    private readonly IWakeOnLanService _wol;
    private readonly IRemoteAccessService _remote;
    private readonly ISystemBrowserLauncher _systemBrowser;
    private readonly IConfigurationService _config;
    private readonly IServiceProvider _services;
    private CancellationTokenSource? _scanCts;
    private CancellationTokenSource? _portCts;
    private CancellationTokenSource? _inspectCts;

    public override string Title => "Scanner de Rede";
    public override string Icon => "📡";

    public ObservableCollection<NetworkHost> Hosts { get; } = new();
    public ObservableCollection<PortScanResult> OpenPorts { get; } = new();

    [ObservableProperty]
    private string _subnetPrefix = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanScanPorts))]
    private NetworkHost? _selectedHost;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ScanSubnetCommand))]
    private bool _isScanning;

    [ObservableProperty]
    private bool _isScanningPorts;

    /// <summary>
    /// Quando ligado, o scan de portas varre TODAS as portas TCP (1–65535) em
    /// vez do range configurado.
    /// </summary>
    [ObservableProperty]
    private bool _scanAllPorts;

    /// <summary>Início do range de portas (padrão vindo das preferências).</summary>
    [ObservableProperty]
    private int _portRangeStart = 1;

    /// <summary>Fim do range de portas (padrão vindo das preferências).</summary>
    [ObservableProperty]
    private int _portRangeEnd = 1024;

    [ObservableProperty]
    private string _status = "Pronto.";

    // Inspeção aprofundada do host selecionado.
    [ObservableProperty] private bool _isInspecting;
    [ObservableProperty] private HostDetails? _hostDetails;

    /// <summary>True se o Winbox foi localizado no sistema.</summary>
    [ObservableProperty] private bool _isWinboxAvailable;

    public bool CanScanPorts => SelectedHost is not null && !IsScanningPorts;

    public NetworkScannerViewModel(
        INetworkScannerService scanner,
        IPortScannerService portScanner,
        INetworkInterfaceService networkInterface,
        IPingService pingService,
        IHostInspector inspector,
        IWakeOnLanService wol,
        IRemoteAccessService remote,
        ISystemBrowserLauncher systemBrowser,
        IConfigurationService config,
        IServiceProvider services)
    {
        _scanner = scanner;
        _portScanner = portScanner;
        _networkInterface = networkInterface;
        _pingService = pingService;
        _inspector = inspector;
        _wol = wol;
        _remote = remote;
        _systemBrowser = systemBrowser;
        _config = config;
        _services = services;
        IsWinboxAvailable = _remote.IsWinboxAvailable;
        // O serviço espera o prefixo terminado em ponto (ex.: "192.168.0.").
        SubnetPrefix = _networkInterface.GetSubnetPrefix() ?? "192.168.0.";

        // Range de portas padrão vem das preferências do usuário.
        try
        {
            var s = _config.LoadSettingsAsync().GetAwaiter().GetResult();
            PortRangeStart = s.PortScanStart;
            PortRangeEnd = s.PortScanEnd;
        }
        catch
        {
            PortRangeStart = 1;
            PortRangeEnd = 1024;
        }
    }

    /// <summary>Garante o prefixo no formato "a.b.c." aceito pelo scanner.</summary>
    private static string NormalizePrefix(string prefix)
    {
        var trimmed = prefix.Trim();
        return trimmed.EndsWith('.') ? trimmed : trimmed + ".";
    }

    private bool CanStartScan() => !IsScanning;

    [RelayCommand(CanExecute = nameof(CanStartScan), IncludeCancelCommand = true)]
    private async Task ScanSubnetAsync(CancellationToken token)
    {
        if (string.IsNullOrWhiteSpace(SubnetPrefix))
        {
            Status = "Informe um prefixo de sub-rede válido (ex.: 192.168.0).";
            return;
        }

        var prefix = NormalizePrefix(SubnetPrefix);
        _scanCts = CancellationTokenSource.CreateLinkedTokenSource(token);
        IsScanning = true;
        Hosts.Clear();
        OpenPorts.Clear();
        Status = $"Varrendo {prefix}1 – {prefix}254…";

        var progress = new Progress<NetworkHost>(host =>
        {
            if (host.IsOnline)
            {
                Hosts.Add(host);
            }
        });

        try
        {
            await _scanner.ScanSubnetAsync(
                prefix,
                probeWebPorts: true,
                progress: progress,
                ct: _scanCts.Token);
            var withWeb = Hosts.Count(h => h.HasWeb);
            Status = $"Varredura concluída: {Hosts.Count} host(s) online" +
                     (withWeb > 0 ? $", {withWeb} com interface web." : ".");
        }
        catch (OperationCanceledException)
        {
            Status = $"Varredura cancelada ({Hosts.Count} host(s) encontrados).";
        }
        catch (Exception ex)
        {
            Status = $"Erro na varredura: {ex.Message}";
        }
        finally
        {
            IsScanning = false;
            _scanCts?.Dispose();
            _scanCts = null;
        }
    }

    [RelayCommand(CanExecute = nameof(CanScanPorts))]
    private async Task ScanPortsAsync()
    {
        if (SelectedHost is null)
        {
            return;
        }

        _portCts = new CancellationTokenSource();
        IsScanningPorts = true;
        OpenPorts.Clear();
        var ip = SelectedHost.IpAddress;

        // Modo completo: varre TODAS as portas TCP (1–65535). Mais lento e mais
        // "barulhento" na rede, mas não deixa nenhuma porta de fora. Usa timeout
        // curto e concorrência alta para compensar o volume. Modo padrão: o range
        // configurado nas preferências (ex.: 1–1024).
        IEnumerable<int> portsToScan;
        int timeoutMs;
        int maxConcurrency;
        if (ScanAllPorts)
        {
            portsToScan = Enumerable.Range(1, 65535);
            timeoutMs = 300;
            maxConcurrency = 600;
            Status = $"Varrendo TODAS as portas (1–65535) de {ip}… isso pode levar alguns minutos.";
        }
        else
        {
            var start = Math.Clamp(PortRangeStart, 1, 65535);
            var end = Math.Clamp(PortRangeEnd, start, 65535);
            portsToScan = Enumerable.Range(start, end - start + 1);
            timeoutMs = 400;
            maxConcurrency = 200;
            Status = $"Varrendo portas {start}–{end} de {ip}…";
        }

        var progress = new Progress<PortScanResult>(result =>
        {
            if (result.IsOpen)
            {
                OpenPorts.Add(result);
            }
        });

        try
        {
            await _portScanner.ScanPortsAsync(
                ip,
                portsToScan,
                timeoutMs: timeoutMs,
                maxConcurrency: maxConcurrency,
                progress: progress,
                ct: _portCts.Token);
            Status = $"{OpenPorts.Count} porta(s) aberta(s) em {ip}" +
                     (ScanAllPorts ? " (varredura completa 1–65535)." : ".");
        }
        catch (OperationCanceledException)
        {
            Status = "Scan de portas cancelado.";
        }
        catch (Exception ex)
        {
            Status = $"Erro no scan de portas: {ex.Message}";
        }
        finally
        {
            IsScanningPorts = false;
            _portCts?.Dispose();
            _portCts = null;
        }
    }

    [RelayCommand]
    private void CancelPortScan() => _portCts?.Cancel();

    // ------------------------------------------------------ Menu de contexto
    [RelayCommand]
    private void OpenHttpInternal(NetworkHost? host)
    {
        var target = host ?? SelectedHost;
        if (target is null)
        {
            return;
        }

        var navigator = _services.GetService(typeof(IAppNavigator)) as IAppNavigator;
        navigator?.OpenInInternalBrowser($"http://{target.IpAddress}");
    }

    [RelayCommand]
    private void OpenHttpsInternal(NetworkHost? host)
    {
        var target = host ?? SelectedHost;
        if (target is null)
        {
            return;
        }

        var navigator = _services.GetService(typeof(IAppNavigator)) as IAppNavigator;
        navigator?.OpenInInternalBrowser($"https://{target.IpAddress}");
    }

    [RelayCommand]
    private void OpenHttpsSystem(NetworkHost? host)
    {
        var target = host ?? SelectedHost;
        if (target is not null)
        {
            _systemBrowser.Open($"https://{target.IpAddress}");
        }
    }

    [RelayCommand]
    private async Task CopyIp(NetworkHost? host)
    {
        var target = host ?? SelectedHost;
        if (target is null)
        {
            return;
        }

        var clipboard = (Application.Current?.ApplicationLifetime
            as Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime)?
            .MainWindow?.Clipboard;

        if (clipboard is not null)
        {
            await clipboard.SetTextAsync(target.IpAddress);
            Status = $"IP {target.IpAddress} copiado.";
        }
    }

    [RelayCommand]
    private async Task ScanPortsOf(NetworkHost? host)
    {
        if (host is not null)
        {
            SelectedHost = host;
        }

        if (CanScanPorts)
        {
            await ScanPortsAsync();
        }
    }

    [RelayCommand]
    private async Task PingHost(NetworkHost? host)
    {
        var target = host ?? SelectedHost;
        if (target is null)
        {
            return;
        }

        Status = $"Pingando {target.IpAddress}…";
        try
        {
            var result = await _pingService.PingAsync(target.IpAddress);
            Status = result.Success
                ? $"Ping {target.IpAddress}: {result.RoundtripTimeMs} ms (TTL {result.Ttl})."
                : $"Ping {target.IpAddress}: sem resposta ({result.ErrorMessage}).";
        }
        catch (Exception ex)
        {
            Status = $"Erro ao pingar {target.IpAddress}: {ex.Message}";
        }
    }

    // ------------------------------------------------- Inspeção aprofundada
    [RelayCommand]
    private async Task Inspect(NetworkHost? host)
    {
        var target = host ?? SelectedHost;
        if (target is null)
        {
            return;
        }

        SelectedHost = target;
        _inspectCts?.Cancel();
        _inspectCts = new CancellationTokenSource();
        IsInspecting = true;
        HostDetails = null;
        Status = $"Inspecionando {target.IpAddress} (serviços, SO, MAC)…";

        try
        {
            HostDetails = await _inspector.InspectAsync(target.IpAddress, ct: _inspectCts.Token);
            var svc = HostDetails.Services.Count;
            Status = $"{target.IpAddress}: {svc} serviço(s), SO {HostDetails.OsGuess ?? "?"}, " +
                     $"MAC {HostDetails.MacAddress ?? "n/d"}.";
        }
        catch (OperationCanceledException)
        {
            Status = "Inspeção cancelada.";
        }
        catch (Exception ex)
        {
            Status = $"Erro na inspeção: {ex.Message}";
        }
        finally
        {
            IsInspecting = false;
        }
    }

    [RelayCommand]
    private async Task WakeOnLan(NetworkHost? host)
    {
        // Usa o MAC do detalhe inspecionado ou o do host, se já conhecido.
        var mac = HostDetails?.MacAddress ?? (host ?? SelectedHost)?.MacAddress;
        if (string.IsNullOrWhiteSpace(mac))
        {
            Status = "MAC desconhecido — rode a Inspeção primeiro para obter o MAC.";
            return;
        }

        var broadcast = BuildBroadcast();
        var ok = await _wol.WakeAsync(mac, broadcast);
        Status = ok
            ? $"Magic packet (Wake-on-LAN) enviado para {mac}."
            : "Falha ao enviar o Wake-on-LAN.";
    }

    /// <summary>Deriva o broadcast da sub-rede atual (ex.: 192.168.0.255).</summary>
    private string? BuildBroadcast()
    {
        var prefix = _networkInterface.GetSubnetPrefix();
        return string.IsNullOrWhiteSpace(prefix) ? null : $"{prefix.TrimEnd('.')}.255";
    }

    // ------------------------------------------------------- Acesso remoto
    [RelayCommand]
    private void OpenSsh(NetworkHost? host)
    {
        var target = host ?? SelectedHost;
        if (target is null)
        {
            return;
        }

        // Usa a porta SSH detectada na inspeção, se diferente da padrão.
        var port = PortForService("SSH") ?? 22;
        var r = _remote.OpenSsh(target.IpAddress, port);
        Status = r.Launched ? $"SSH aberto: {r.Command}" : $"Falha no SSH: {r.Error}";
    }

    /// <summary>Abre o terminal SSH embutido (funciona inclusive no Android).</summary>
    [RelayCommand]
    private void OpenSshEmbedded(NetworkHost? host)
    {
        var target = host ?? SelectedHost;
        if (target is null)
        {
            return;
        }

        var port = PortForService("SSH") ?? 22;
        var navigator = _services.GetService(typeof(IAppNavigator)) as IAppNavigator;
        navigator?.OpenTerminal(target.IpAddress, port);
        Status = $"Terminal SSH embutido aberto para {target.IpAddress}:{port}.";
    }

    [RelayCommand]
    private void OpenTelnet(NetworkHost? host)
    {
        var target = host ?? SelectedHost;
        if (target is null)
        {
            return;
        }

        var port = PortForService("Telnet") ?? 23;
        var r = _remote.OpenTelnet(target.IpAddress, port);
        Status = r.Launched ? $"Telnet aberto: {r.Command}" : $"Falha no Telnet: {r.Error}";
    }

    [RelayCommand]
    private void OpenWinboxByIp(NetworkHost? host)
    {
        var target = host ?? SelectedHost;
        if (target is null)
        {
            return;
        }

        var r = _remote.OpenWinbox(target.IpAddress);
        Status = r.Launched ? $"Winbox (IP) aberto para {target.IpAddress}." : $"Winbox: {r.Error}";
    }

    [RelayCommand]
    private void OpenWinboxByMac(NetworkHost? host)
    {
        var target = host ?? SelectedHost;
        // Prefere o MAC da inspeção; cai no MAC do host, se houver.
        var mac = HostDetails?.MacAddress ?? target?.MacAddress;
        if (string.IsNullOrWhiteSpace(mac))
        {
            Status = "MAC desconhecido — rode a Inspeção primeiro para conectar o Winbox por MAC.";
            return;
        }

        var r = _remote.OpenWinbox(mac);
        Status = r.Launched ? $"Winbox (MAC) aberto para {mac}." : $"Winbox: {r.Error}";
    }

    /// <summary>Procura, nos serviços inspecionados, a porta de um serviço pelo nome.</summary>
    private int? PortForService(string serviceName)
    {
        var svc = HostDetails?.Services
            .FirstOrDefault(s => s.ServiceName.Contains(serviceName, StringComparison.OrdinalIgnoreCase));
        return svc?.Port;
    }
}
