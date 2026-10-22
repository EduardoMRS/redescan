namespace RedeScan.Core.Models;

/// <summary>Serviço identificado em uma porta, com banner quando disponível.</summary>
public record ServiceInfo
{
    public int Port { get; init; }
    public string ServiceName { get; init; } = string.Empty;
    /// <summary>Produto/versão detectado via banner (ex.: "OpenSSH 8.9", "nginx").</summary>
    public string? Product { get; init; }
    /// <summary>Trecho do banner bruto capturado (limitado).</summary>
    public string? Banner { get; init; }
}

/// <summary>
/// Perfil completo de um host, resultado do scan aprofundado: nome, MAC,
/// fabricante, estimativa de SO e os serviços rodando em cada porta aberta.
/// </summary>
public record HostDetails
{
    public string IpAddress { get; init; } = string.Empty;
    public string? Hostname { get; init; }
    public string? MacAddress { get; init; }
    public string? Vendor { get; init; }

    /// <summary>Estimativa do SO (heurística por TTL/banners). Pode ser incerto.</summary>
    public string? OsGuess { get; init; }

    public long RoundtripTimeMs { get; init; }
    public int Ttl { get; init; }

    public IReadOnlyList<ServiceInfo> Services { get; init; } = Array.Empty<ServiceInfo>();

    /// <summary>True se temos MAC — pré-requisito para Wake-on-LAN.</summary>
    public bool SupportsWakeOnLan => !string.IsNullOrWhiteSpace(MacAddress);
}
