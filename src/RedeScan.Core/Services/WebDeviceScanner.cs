using System.Collections.Concurrent;
using RedeScan.Core.Models;

namespace RedeScan.Core.Services;

/// <summary>
/// Implementação do <see cref="IWebDeviceScanner"/>. Reusa o ping sweep do
/// <see cref="INetworkScannerService"/> e o scan de portas do
/// <see cref="IPortScannerService"/> para achar hosts com interface web.
/// </summary>
public sealed class WebDeviceScanner : IWebDeviceScanner
{
    private readonly INetworkScannerService _networkScanner;
    private readonly IPortScannerService _portScanner;
    private readonly INetworkInterfaceService _networkInterface;
    private readonly IArpService? _arpService;
    private readonly IOuiVendorLookup? _vendorLookup;

    // Portas web mais comuns em roteadores, ONUs, câmeras e serviços locais.
    private static readonly int[] DefaultWebPorts =
        { 80, 443, 8080, 8443, 8000, 8090, 8888, 9000, 9090 };

    public IReadOnlyList<int> WebPorts => DefaultWebPorts;

    public WebDeviceScanner(
        INetworkScannerService networkScanner,
        IPortScannerService portScanner,
        INetworkInterfaceService networkInterface,
        IArpService? arpService = null,
        IOuiVendorLookup? vendorLookup = null)
    {
        _networkScanner = networkScanner;
        _portScanner = portScanner;
        _networkInterface = networkInterface;
        _arpService = arpService;
        _vendorLookup = vendorLookup;
    }

    public async Task<IReadOnlyList<WebDevice>> ScanAsync(
        string subnetPrefix,
        IProgress<WebDevice>? progress = null,
        CancellationToken ct = default)
    {
        var prefix = subnetPrefix.EndsWith('.') ? subnetPrefix : subnetPrefix + ".";
        var gateways = GetGatewayIps();
        var found = new ConcurrentBag<WebDevice>();

        // 1) Descobre hosts online (ping sweep).
        var hosts = await _networkScanner.ScanSubnetAsync(prefix, ct: ct);

        // 2) Para cada host, procura portas web abertas (em paralelo controlado).
        var options = new ParallelOptions
        {
            MaxDegreeOfParallelism = 16,
            CancellationToken = ct
        };

        await Parallel.ForEachAsync(hosts, options, async (host, token) =>
        {
            var openWebPorts = new List<int>();
            foreach (var port in DefaultWebPorts)
            {
                var res = await _portScanner.ScanSinglePortAsync(host.IpAddress, port, 600, token);
                if (res.IsOpen)
                {
                    openWebPorts.Add(port);
                }
            }

            if (openWebPorts.Count == 0)
            {
                return;
            }

            string? vendor = host.Vendor;
            if (vendor is null && _arpService is not null)
            {
                var mac = await _arpService.GetMacAddressAsync(host.IpAddress, token);
                vendor = _vendorLookup?.Lookup(mac);
            }

            var device = new WebDevice
            {
                IpAddress = host.IpAddress,
                Hostname = host.Hostname,
                Vendor = vendor,
                WebPorts = openWebPorts,
                IsLikelyRouter = gateways.Contains(host.IpAddress) || IsCommonGatewayHost(host.IpAddress)
            };

            found.Add(device);
            progress?.Report(device);
        });

        return found
            .OrderByDescending(d => d.IsLikelyRouter)
            .ThenBy(d => d.IpAddress)
            .ToList();
    }

    public async Task<IReadOnlyList<WebDevice>> FindRoutersAsync(CancellationToken ct = default)
    {
        var prefix = _networkInterface.GetSubnetPrefix();
        var candidates = new HashSet<string>(GetGatewayIps());

        if (!string.IsNullOrWhiteSpace(prefix))
        {
            // IPs de gateway mais comuns na faixa detectada.
            foreach (var last in new[] { 1, 254, 100, 10 })
            {
                candidates.Add($"{prefix}{last}");
            }
        }

        var results = new ConcurrentBag<WebDevice>();
        var options = new ParallelOptions { MaxDegreeOfParallelism = 8, CancellationToken = ct };

        await Parallel.ForEachAsync(candidates, options, async (ip, token) =>
        {
            var openWebPorts = new List<int>();
            foreach (var port in new[] { 80, 443, 8080, 8443 })
            {
                var res = await _portScanner.ScanSinglePortAsync(ip, port, 700, token);
                if (res.IsOpen)
                {
                    openWebPorts.Add(port);
                }
            }

            if (openWebPorts.Count > 0)
            {
                results.Add(new WebDevice
                {
                    IpAddress = ip,
                    WebPorts = openWebPorts,
                    IsLikelyRouter = true
                });
            }
        });

        return results.OrderBy(d => d.IpAddress).ToList();
    }

    private HashSet<string> GetGatewayIps()
    {
        var set = new HashSet<string>();
        if (_networkInterface.GetDefaultGateway() is { } gw)
        {
            set.Add(gw.ToString());
        }

        foreach (var (_, _, gateway) in _networkInterface.GetAllActiveInterfaces())
        {
            if (gateway is not null)
            {
                set.Add(gateway.ToString());
            }
        }

        return set;
    }

    private static bool IsCommonGatewayHost(string ip)
    {
        // Heurística: último octeto .1 ou .254 costuma ser gateway/ONU.
        var lastDot = ip.LastIndexOf('.');
        if (lastDot < 0)
        {
            return false;
        }

        var last = ip[(lastDot + 1)..];
        return last is "1" or "254";
    }
}
