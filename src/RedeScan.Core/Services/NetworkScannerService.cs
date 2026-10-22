using System.Collections.Concurrent;
using System.Net;
using RedeScan.Core.Models;

namespace RedeScan.Core.Services;

public class NetworkScannerService : INetworkScannerService
{
    private readonly IPingService _pingService;

    public NetworkScannerService(IPingService pingService)
    {
        _pingService = pingService;
    }

    public async Task<IReadOnlyList<NetworkHost>> ScanSubnetAsync(
        string subnetPrefix, 
        int startHost = 1, 
        int endHost = 254, 
        int timeoutMs = 800, 
        int maxConcurrency = 40, 
        bool resolveDns = true, 
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

                var host = new NetworkHost
                {
                    IpAddress = targetIp,
                    Hostname = hostname,
                    RoundtripTimeMs = pingResult.RoundtripTimeMs,
                    IsOnline = true,
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
}
