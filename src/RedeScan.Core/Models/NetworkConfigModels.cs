namespace RedeScan.Core.Models;

/// <summary>Modo de atribuição de endereço de uma interface.</summary>
public enum IpAssignmentMode
{
    /// <summary>Dinâmico (DHCP).</summary>
    Dynamic,
    /// <summary>Estático (IP/máscara/gateway definidos manualmente).</summary>
    Static
}

/// <summary>
/// Requisição de alteração de IP de uma interface. Campos estáticos só são
/// usados quando <see cref="Mode"/> é <see cref="IpAssignmentMode.Static"/>.
/// </summary>
public sealed class IpConfigRequest
{
    public string InterfaceName { get; set; } = string.Empty;
    public IpAssignmentMode Mode { get; set; } = IpAssignmentMode.Dynamic;

    /// <summary>Endereço no formato CIDR, ex.: "192.168.0.50/24".</summary>
    public string? AddressCidr { get; set; }
    public string? Gateway { get; set; }

    /// <summary>Servidores DNS (opcional). Ex.: "8.8.8.8", "1.1.1.1".</summary>
    public List<string> DnsServers { get; set; } = new();
}

/// <summary>Requisição para criar/ativar uma conexão PPPoE.</summary>
public sealed class PppoeRequest
{
    public string InterfaceName { get; set; } = string.Empty;
    public string ConnectionName { get; set; } = "RedeScan-PPPoE";
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

/// <summary>
/// Resultado de uma operação de rede: o comando que seria/foi executado, se
/// aplicado, e a saída/erro. Permite à UI mostrar o comando antes de aplicar.
/// </summary>
public sealed record NetworkCommandResult
{
    public string Command { get; init; } = string.Empty;
    public bool Applied { get; init; }
    public bool Success { get; init; }
    public string? Output { get; init; }
    public string? Error { get; init; }
    public bool RequiresPrivilege { get; init; } = true;
}
