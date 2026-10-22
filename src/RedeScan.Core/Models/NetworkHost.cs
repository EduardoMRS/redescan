namespace RedeScan.Core.Models;

public record NetworkHost
{
    public string IpAddress { get; init; } = string.Empty;
    public string? Hostname { get; init; }
    public long RoundtripTimeMs { get; init; }
    public bool IsOnline { get; init; }
    public string? MacAddress { get; init; }
    public string? Vendor { get; init; }
    public DateTime DiscoveredAt { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// Portas web (HTTP/HTTPS) abertas encontradas na sonda rápida (ex.: 80, 443,
    /// 8080). Vazio quando a sonda web não foi executada ou nada foi encontrado.
    /// </summary>
    public IReadOnlyList<int> WebPorts { get; init; } = Array.Empty<int>();

    /// <summary>Indícios de que o host é provavelmente um roteador/ONU.</summary>
    public bool IsLikelyRouter { get; init; }

    /// <summary>True se a sonda rápida encontrou alguma porta web aberta.</summary>
    public bool HasWeb => WebPorts.Count > 0;

    /// <summary>Resumo das portas web para exibição (ex.: "80, 443").</summary>
    public string WebSummary => WebPorts.Count == 0 ? string.Empty : string.Join(", ", WebPorts);

    /// <summary>
    /// URL web preferencial (prioriza HTTPS/443, depois HTTP/80, depois a
    /// primeira porta encontrada). Vazio quando não há porta web.
    /// </summary>
    public string? PreferredWebUrl
    {
        get
        {
            if (WebPorts.Count == 0)
            {
                return null;
            }

            if (WebPorts.Contains(443))
            {
                return $"https://{IpAddress}";
            }

            if (WebPorts.Contains(80))
            {
                return $"http://{IpAddress}";
            }

            var port = WebPorts[0];
            var scheme = port is 8443 or 9443 ? "https" : "http";
            return $"{scheme}://{IpAddress}:{port}";
        }
    }
}
