using System.Collections.Concurrent;
using System.Net;
using RedeScan.Core.Models;

namespace RedeScan.Core.Services;

public class NetworkScannerService : INetworkScannerService
{
    private readonly IPingService _pingService;
    private readonly IArpService? _arpService;
    private readonly IOuiVendorLookup? _vendorLookup;
    private readonly IPortScannerService? _portScanner;

    /// <summary>Portas consideradas "web" na sonda rápida (mesmo conjunto do painel).</summary>
    private static readonly int[] WebPorts = { 80, 443, 8080, 8443, 8000, 8090, 8888, 9000, 9090 };

    public NetworkScannerService(
        IPingService pingService,
        IArpService? arpService = null,
        IOuiVendorLookup? vendorLookup = null,
        IPortScannerService? portScanner = null)
    {
        _pingService = pingService;
        _arpService = arpService;
        _vendorLookup = vendorLookup;
        _portScanner = portScanner;
    }

    public async Task<IReadOnlyList<NetworkHost>> ScanSubnetAsync(
        string subnetPrefix, 
        int startHost = 1, 
        int endHost = 254, 
        int timeoutMs = 800, 
        int maxConcurrency = 40, 
        bool resolveDns = true, 
        bool probeWebPorts = false,
        IProgress<NetworkHost>? progress = null, 
        CancellationToken ct = default)
    {
        var discoveredHosts = new ConcurrentBag<NetworkHost>();
        var hostsToScan = Enumerable.Range(startHost, Math.Max(0, endHost - startHost + 1)).ToList();
        var parallelOptions = new ParallelOptions
        {
            MaxDegreeOfParallelism = Math.Max(1, maxConcurrency),
            CancellationToken = ct
        };

        await Parallel.ForEachAsync(hostsToScan, parallelOptions, async (hostNum, token) =>
        {
            var targetIp = $"{subnetPrefix}{hostNum}";
            var pingResult = await _pingService.PingAsync(targetIp, timeoutMs, null, token);

            if (pingResult.Success)
            {
                string? hostname = null;
                if (resolveDns)
                {
                    try
                    {
                        var dnsTask = Dns.GetHostEntryAsync(targetIp);
                        if (await Task.WhenAny(dnsTask, Task.Delay(250, token)) == dnsTask)
                        {
                            hostname = (await dnsTask).HostName;
                        }
                    }
                    catch
                    {
                        // Fallback quando não há PTR DNS registrado
                    }
                }

                string? mac = null;
                string? vendor = null;
                if (_arpService is not null)
                {
                    mac = await _arpService.GetMacAddressAsync(targetIp, token);
                    vendor = _vendorLookup?.Lookup(mac);
                }

                // Sonda web rápida (igual à do painel): só as portas web comuns,
                // em paralelo, sem banner grabbing. Barata o suficiente para rodar
                // durante o próprio sweep e dar "mais dados" sem perder velocidade.
                IReadOnlyList<int> webPorts = Array.Empty<int>();
                if (probeWebPorts && _portScanner is not null)
                {
                    webPorts = await ProbeWebPortsAsync(targetIp, token);
                }

                var host = new NetworkHost
                {
                    IpAddress = targetIp,
                    Hostname = hostname,
                    RoundtripTimeMs = pingResult.RoundtripTimeMs,
                    IsOnline = true,
                    MacAddress = mac,
                    Vendor = vendor,
                    WebPorts = webPorts,
                    IsLikelyRouter = IsCommonGatewayHost(targetIp),
                    DiscoveredAt = DateTime.UtcNow
                };

                discoveredHosts.Add(host);
                progress?.Report(host);
            }
        });

        return discoveredHosts
            .OrderBy(h => IPAddress.TryParse(h.IpAddress, out var ip) ? BitConverter.ToUInt32(ip.GetAddressBytes().Reverse().ToArray(), 0) : 0)
            .ToList();
    }

    /// <summary>Testa as portas web comuns em paralelo e devolve as abertas.</summary>
    private async Task<IReadOnlyList<int>> ProbeWebPortsAsync(string ip, CancellationToken ct)
    {
        var open = new ConcurrentBag<int>();
        var options = new ParallelOptions { MaxDegreeOfParallelism = 9, CancellationToken = ct };
        try
        {
            await Parallel.ForEachAsync(WebPorts, options, async (port, token) =>
            {
                var res = await _portScanner!.ScanSinglePortAsync(ip, port, 600, token);
                if (res.IsOpen)
                {
                    open.Add(port);
                }
            });
        }
        catch (OperationCanceledException)
        {
            // cancelado — devolve o que já encontrou
        }

        return open.OrderBy(p => p).ToList();
    }

    /// <summary>Heurística: último octeto .1 ou .254 costuma ser gateway/ONU.</summary>
    private static bool IsCommonGatewayHost(string ip)
    {
        var lastDot = ip.LastIndexOf('.');
        if (lastDot < 0)
        {
            return false;
        }

        var last = ip[(lastDot + 1)..];
        return last is "1" or "254";
    }
}
