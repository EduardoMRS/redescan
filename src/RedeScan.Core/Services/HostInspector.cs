using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using RedeScan.Core.Models;

namespace RedeScan.Core.Services;

/// <summary>
/// Implementação do <see cref="IHostInspector"/>. Identifica serviços por
/// banner grabbing (lê a resposta inicial da porta, ou envia uma requisição
/// HTTP mínima), estima o SO pelo TTL do ping e junta nome/MAC/fabricante.
/// </summary>
public sealed partial class HostInspector : IHostInspector
{
    private readonly IPingService _ping;
    private readonly IPortScannerService _portScanner;
    private readonly IArpService _arp;
    private readonly IOuiVendorLookup _vendor;

    public HostInspector(
        IPingService ping,
        IPortScannerService portScanner,
        IArpService arp,
        IOuiVendorLookup vendor)
    {
        _ping = ping;
        _portScanner = portScanner;
        _arp = arp;
        _vendor = vendor;
    }

    public async Task<HostDetails> InspectAsync(
        string ipAddress,
        IEnumerable<int>? ports = null,
        int timeoutMs = 800,
        CancellationToken ct = default)
    {
        var portList = (ports ?? _portScanner.KnownPorts.Keys).Distinct().ToList();

        // Ping para RTT + TTL (base da estimativa de SO).
        var ping = await _ping.PingAsync(ipAddress, 1000, null, ct);

        // Nome (DNS reverso), MAC e fabricante.
        var hostname = await ResolveHostnameAsync(ipAddress, ct);
        var mac = await _arp.GetMacAddressAsync(ipAddress, ct);
        var vendor = _vendor.Lookup(mac);

        // Serviços: só nas portas abertas, com banner.
        var services = new List<ServiceInfo>();
        foreach (var port in portList)
        {
            ct.ThrowIfCancellationRequested();
            var scan = await _portScanner.ScanSinglePortAsync(ipAddress, port, timeoutMs, ct);
            if (!scan.IsOpen)
            {
                continue;
            }

            var (product, banner) = await GrabBannerAsync(ipAddress, port, timeoutMs, ct);
            services.Add(new ServiceInfo
            {
                Port = port,
                ServiceName = scan.ServiceName,
                Product = product,
                Banner = banner
            });
        }

        var osGuess = GuessOs(ping.Success ? ping.Ttl : 0, services);

        return new HostDetails
        {
            IpAddress = ipAddress,
            Hostname = hostname,
            MacAddress = mac,
            Vendor = vendor,
            OsGuess = osGuess,
            RoundtripTimeMs = ping.RoundtripTimeMs,
            Ttl = ping.Ttl,
            Services = services
        };
    }

    private static async Task<string?> ResolveHostnameAsync(string ip, CancellationToken ct)
    {
        try
        {
            var task = Dns.GetHostEntryAsync(ip);
            if (await Task.WhenAny(task, Task.Delay(400, ct)) == task)
            {
                return (await task).HostName;
            }
        }
        catch
        {
            // sem PTR
        }
        return null;
    }

    /// <summary>
    /// Conecta na porta e tenta ler o banner. Para portas HTTP, envia um
    /// HEAD mínimo para obter o header Server. Retorna (produto, banner).
    /// </summary>
    private static async Task<(string? Product, string? Banner)> GrabBannerAsync(
        string ip, int port, int timeoutMs, CancellationToken ct)
    {
        try
        {
            using var client = new TcpClient();
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(timeoutMs);
            await client.ConnectAsync(ip, port, cts.Token);

            using var stream = client.GetStream();
            stream.ReadTimeout = timeoutMs;
            stream.WriteTimeout = timeoutMs;

            var isHttp = port is 80 or 8080 or 8000 or 8090 or 8888 or 9000 or 9090;
            var isHttps = port is 443 or 8443 or 9443;

            if (isHttp)
            {
                var req = Encoding.ASCII.GetBytes(
                    $"HEAD / HTTP/1.1\r\nHost: {ip}\r\nConnection: close\r\n\r\n");
                await stream.WriteAsync(req, cts.Token);
            }
            // HTTPS exigiria handshake TLS — não fazemos banner aqui, só marcamos.

            var buffer = new byte[2048];
            using var readCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            readCts.CancelAfter(timeoutMs);

            int read;
            try
            {
                read = await stream.ReadAsync(buffer, readCts.Token);
            }
            catch
            {
                read = 0;
            }

            if (read <= 0)
            {
                return (isHttps ? "TLS/HTTPS" : null, null);
            }

            var raw = Encoding.ASCII.GetString(buffer, 0, read).Trim();
            var product = ExtractProduct(raw, port);
            var banner = raw.Length > 300 ? raw[..300] : raw;
            return (product, banner);
        }
        catch
        {
            return (null, null);
        }
    }

    [GeneratedRegex(@"(?im)^Server:\s*(.+)$")]
    private static partial Regex ServerHeaderRegex();

    private static string? ExtractProduct(string banner, int port)
    {
        if (string.IsNullOrWhiteSpace(banner))
        {
            return null;
        }

        // HTTP: header Server.
        var m = ServerHeaderRegex().Match(banner);
        if (m.Success)
        {
            return m.Groups[1].Value.Trim();
        }

        // SSH/FTP/SMTP costumam iniciar com uma linha identificável.
        var firstLine = banner.Split('\n')[0].Trim();
        if (firstLine.StartsWith("SSH-", StringComparison.OrdinalIgnoreCase) ||
            firstLine.StartsWith("220", StringComparison.Ordinal) ||    // FTP/SMTP
            firstLine.StartsWith("RTSP", StringComparison.OrdinalIgnoreCase))
        {
            return firstLine.Length > 80 ? firstLine[..80] : firstLine;
        }

        return null;
    }

    /// <summary>
    /// Estimativa de SO por TTL (heurística): ~64 Linux/Android/Unix,
    /// ~128 Windows, ~255 equipamentos de rede. Refina com banners.
    /// </summary>
    private static string? GuessOs(int ttl, IReadOnlyList<ServiceInfo> services)
    {
        var banners = string.Join(" ", services.Select(s => s.Product ?? string.Empty)).ToLowerInvariant();

        if (banners.Contains("windows") || banners.Contains("microsoft") || banners.Contains("iis"))
        {
            return "Windows (banner)";
        }
        if (banners.Contains("ubuntu")) return "Linux/Ubuntu (banner)";
        if (banners.Contains("debian")) return "Linux/Debian (banner)";
        if (banners.Contains("mikrotik") || banners.Contains("routeros")) return "MikroTik RouterOS";

        if (ttl <= 0)
        {
            return null;
        }

        // Normaliza para o TTL inicial mais próximo (o valor cai 1 por salto).
        return ttl switch
        {
            <= 64 => "Linux/Android/Unix (TTL ~64)",
            <= 128 => "Windows (TTL ~128)",
            _ => "Equipamento de rede (TTL ~255)"
        };
    }
}
