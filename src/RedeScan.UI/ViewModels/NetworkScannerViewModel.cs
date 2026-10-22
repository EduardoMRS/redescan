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
    private readonly ISystemBrowserLauncher _systemBrowser;
    private readonly IServiceProvider _services;
    private CancellationTokenSource? _scanCts;
    private CancellationTokenSource? _portCts;

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

    [ObservableProperty]
    private string _status = "Pronto.";

    public bool CanScanPorts => SelectedHost is not null && !IsScanningPorts;

    public NetworkScannerViewModel(
        INetworkScannerService scanner,
        IPortScannerService portScanner,
        INetworkInterfaceService networkInterface,
        IPingService pingService,
        ISystemBrowserLauncher systemBrowser,
        IServiceProvider services)
    {
        _scanner = scanner;
        _portScanner = portScanner;
        _networkInterface = networkInterface;
        _pingService = pingService;
        _systemBrowser = systemBrowser;
        _services = services;
        // O serviço espera o prefixo terminado em ponto (ex.: "192.168.0.").
        SubnetPrefix = _networkInterface.GetSubnetPrefix() ?? "192.168.0.";
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
                progress: progress,
                ct: _scanCts.Token);
            Status = $"Varredura concluída: {Hosts.Count} host(s) online.";
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
        Status = $"Varrendo portas conhecidas de {ip}…";

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
                _portScanner.KnownPorts.Keys,
                progress: progress,
                ct: _portCts.Token);
            Status = $"{OpenPorts.Count} porta(s) aberta(s) em {ip}.";
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
}
