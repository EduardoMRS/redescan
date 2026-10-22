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
/// Requisição para criar uma interface VLAN (802.1Q) sobre uma interface física.
/// A VLAN pode usar DHCP ou um IP estático próprio.
/// </summary>
public sealed class VlanRequest
{
    /// <summary>Interface física que carrega a VLAN (ex.: "eth0", "Ethernet").</summary>
    public string ParentInterface { get; set; } = string.Empty;

    /// <summary>ID da VLAN (1–4094).</summary>
    public int VlanId { get; set; }

    /// <summary>
    /// Nome da conexão/interface VLAN. Se vazio, o padrão é
    /// <c>{ParentInterface}.{VlanId}</c>.
    /// </summary>
    public string? ConnectionName { get; set; }

    /// <summary>Modo de IP da VLAN (DHCP ou estático).</summary>
    public IpAssignmentMode Mode { get; set; } = IpAssignmentMode.Dynamic;

    /// <summary>Endereço CIDR quando estático (ex.: "192.168.10.2/24").</summary>
    public string? AddressCidr { get; set; }

    /// <summary>Gateway quando estático (opcional).</summary>
    public string? Gateway { get; set; }

    /// <summary>Servidores DNS quando estático (opcional).</summary>
    public List<string> DnsServers { get; set; } = new();

    /// <summary>Nome efetivo da conexão VLAN.</summary>
    public string EffectiveName =>
        string.IsNullOrWhiteSpace(ConnectionName)
            ? $"{ParentInterface}.{VlanId}"
            : ConnectionName!;
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
