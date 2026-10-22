using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net.Sockets;
using RedeScan.Core.Models;

namespace RedeScan.Core.Services;

public class PortScannerService : IPortScannerService
{
    private static readonly Dictionary<int, string> DefaultKnownPorts = new()
    {
        { 21, "FTP" },
        { 22, "SSH" },
        { 23, "Telnet" },
        { 25, "SMTP" },
        { 53, "DNS" },
        { 80, "HTTP" },
        { 110, "POP3" },
        { 135, "RPC" },
        { 139, "NetBIOS" },
        { 143, "IMAP" },
        { 443, "HTTPS" },
        { 445, "SMB" },
        { 554, "RTSP" },
        { 993, "IMAPS" },
        { 995, "POP3S" },
        { 1433, "MSSQL" },
        { 1521, "Oracle" },
        { 3306, "MySQL" },
        { 3389, "RDP" },
        { 5000, "UPnP / Synology" },
        { 5432, "PostgreSQL" },
        { 5900, "VNC" },
        { 8000, "HTTP-Alt" },
        { 8080, "HTTP-Proxy" },
        { 8443, "HTTPS-Alt" },
        { 8554, "RTSP-Alt" },
        { 9000, "Portainer / Web" },
        { 9090, "Cockpit / Prometheus" }
    };

    public IReadOnlyDictionary<int, string> KnownPorts => DefaultKnownPorts;

    public async Task<PortScanResult> ScanSinglePortAsync(
        string ipAddress, 
        int port, 
        int timeoutMs = 500, 
        CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        using var client = new TcpClient();
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(timeoutMs);

        try
        {
            await client.ConnectAsync(ipAddress, port, cts.Token);
            sw.Stop();

            DefaultKnownPorts.TryGetValue(port, out var serviceName);

            return new PortScanResult
            {
                IpAddress = ipAddress,
                Port = port,
                IsOpen = true,
                ServiceName = serviceName ?? "Desconhecido",
                ResponseTimeMs = sw.ElapsedMilliseconds
            };
        }
        catch
        {
            sw.Stop();
            DefaultKnownPorts.TryGetValue(port, out var serviceName);

            return new PortScanResult
            {
                IpAddress = ipAddress,
                Port = port,
                IsOpen = false,
                ServiceName = serviceName ?? "Desconhecido",
                ResponseTimeMs = sw.ElapsedMilliseconds
            };
        }
    }

    public async Task<IReadOnlyList<PortScanResult>> ScanPortsAsync(
        string ipAddress, 
        IEnumerable<int> ports, 
        int timeoutMs = 500, 
        int maxConcurrency = 20, 
        IProgress<PortScanResult>? progress = null, 
        CancellationToken ct = default)
    {
        var portList = ports.Distinct().ToList();
        var results = new ConcurrentBag<PortScanResult>();

        var options = new ParallelOptions
        {
            MaxDegreeOfParallelism = Math.Max(1, maxConcurrency),
            CancellationToken = ct
        };

        await Parallel.ForEachAsync(portList, options, async (port, token) =>
        {
            var res = await ScanSinglePortAsync(ipAddress, port, timeoutMs, token);
            results.Add(res);
            progress?.Report(res);
        });

        return results.OrderBy(r => r.Port).ToList();
    }
}
