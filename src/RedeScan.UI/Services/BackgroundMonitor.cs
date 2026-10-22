using System.Threading;
using RedeScan.Core.Services;

namespace RedeScan.UI.Services;

/// <summary>
/// Implementação do <see cref="IBackgroundMonitor"/>. A cada ciclo compara o
/// gateway/sub-rede atual com o anterior (mudança de rede) e, quando a rede está
/// estável, varre a sub-rede para detectar dispositivos novos desde a última
/// verificação.
/// </summary>
public sealed class BackgroundMonitor : IBackgroundMonitor
{
    private readonly INetworkInterfaceService _net;
    private readonly INetworkScannerService _scanner;

    private Timer? _timer;
    private string? _lastSubnet;
    private string? _lastGateway;
    private readonly HashSet<string> _knownHosts = new();
    private int _busy;

    public bool IsRunning { get; private set; }
    public event Action<MonitorNotification>? Notified;

    public BackgroundMonitor(INetworkInterfaceService net, INetworkScannerService scanner)
    {
        _net = net;
        _scanner = scanner;
    }

    public void Start()
    {
        if (IsRunning)
        {
            return;
        }
        IsRunning = true;
        // Primeiro ciclo em 5s; depois a cada 90s.
        _timer = new Timer(_ => _ = TickAsync(), null, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(90));
    }

    public void Stop()
    {
        _timer?.Dispose();
        _timer = null;
        IsRunning = false;
    }

    private async Task TickAsync()
    {
        // Evita sobreposição de ciclos (um scan pode demorar).
        if (Interlocked.Exchange(ref _busy, 1) == 1)
        {
            return;
        }

        try
        {
            var subnet = _net.GetSubnetPrefix();
            var gateway = _net.GetDefaultGateway()?.ToString();

            if (string.IsNullOrWhiteSpace(subnet))
            {
                return; // sem rede
            }

            // Mudança de rede (troca de Wi-Fi/cabo, novo gateway/sub-rede).
            if (_lastSubnet is not null && (subnet != _lastSubnet || gateway != _lastGateway))
            {
                _knownHosts.Clear();
                Notified?.Invoke(new MonitorNotification(
                    MonitorEventKind.NetworkChanged,
                    "Rede alterada",
                    $"Nova rede detectada: {subnet.TrimEnd('.')}.0/24" +
                    (gateway is not null ? $" (gateway {gateway})" : string.Empty)));
            }
            _lastSubnet = subnet;
            _lastGateway = gateway;

            // Varredura para detectar dispositivos novos.
            var hosts = await _scanner.ScanSubnetAsync(subnet);
            var firstRun = _knownHosts.Count == 0;
            foreach (var h in hosts)
            {
                if (_knownHosts.Add(h.IpAddress) && !firstRun)
                {
                    var name = string.IsNullOrWhiteSpace(h.Hostname) ? h.IpAddress : h.Hostname;
                    Notified?.Invoke(new MonitorNotification(
                        MonitorEventKind.NewDevice,
                        "Novo dispositivo na rede",
                        $"{name}" + (h.Vendor is not null ? $" — {h.Vendor}" : string.Empty)));
                }
            }
        }
        catch
        {
            // Monitor é best-effort: nunca quebra a aplicação.
        }
        finally
        {
            Interlocked.Exchange(ref _busy, 0);
        }
    }
}
