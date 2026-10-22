namespace RedeScan.Core.Models;

/// <summary>
/// Um dispositivo na rede que expõe pelo menos uma interface web (HTTP/HTTPS)
/// em alguma porta, independente de qual seja. Usado pelos atalhos do painel.
/// </summary>
public record WebDevice
{
    public string IpAddress { get; init; } = string.Empty;
    public string? Hostname { get; init; }
    public string? Vendor { get; init; }

    /// <summary>Portas web abertas encontradas (ex.: 80, 443, 8080).</summary>
    public IReadOnlyList<int> WebPorts { get; init; } = Array.Empty<int>();

    /// <summary>Indícios de que o dispositivo é provavelmente um roteador/ONU.</summary>
    public bool IsLikelyRouter { get; init; }

    /// <summary>
    /// URL preferencial para abrir no navegador (prioriza HTTPS/443, depois
    /// HTTP/80, depois a primeira porta web encontrada).
    /// </summary>
    public string PreferredUrl
    {
        get
        {
            if (WebPorts.Count == 0)
            {
                return $"http://{IpAddress}";
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

    /// <summary>Rótulo amigável para exibição no atalho.</summary>
    public string DisplayLabel
    {
        get
        {
            var name = string.IsNullOrWhiteSpace(Hostname) ? IpAddress : Hostname;
            var ports = string.Join(", ", WebPorts);
            var tag = IsLikelyRouter ? " • roteador?" : string.Empty;
            return $"{name}  ({ports}){tag}";
        }
    }
}
