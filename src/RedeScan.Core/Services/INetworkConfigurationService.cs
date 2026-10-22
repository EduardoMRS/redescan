using RedeScan.Core.Models;

namespace RedeScan.Core.Services;

/// <summary>
/// Altera a configuração de rede do sistema (IP estático/dinâmico, DNS, PPPoE).
/// Operações são sensíveis e exigem privilégio administrativo. Por segurança, é
/// possível apenas PREVISUALIZAR o comando (<c>apply: false</c>) antes de aplicar.
/// </summary>
public interface INetworkConfigurationService
{
    /// <summary>True se a plataforma atual é suportada (Linux via nmcli / Windows via netsh).</summary>
    bool IsSupported { get; }

    /// <summary>Nome do backend usado ("nmcli", "netsh" ou "não suportado").</summary>
    string Backend { get; }

    /// <summary>
    /// Configura o IP de uma interface (estático ou DHCP) e, opcionalmente, DNS.
    /// Quando <paramref name="apply"/> é false, apenas devolve o comando montado.
    /// </summary>
    Task<NetworkCommandResult> SetIpAsync(IpConfigRequest request, bool apply, CancellationToken ct = default);

    /// <summary>
    /// Define apenas os servidores DNS de uma interface.
    /// </summary>
    Task<NetworkCommandResult> SetDnsAsync(string interfaceName, IReadOnlyList<string> dnsServers, bool apply, CancellationToken ct = default);

    /// <summary>
    /// Cria/ativa uma conexão PPPoE na interface informada.
    /// </summary>
    Task<NetworkCommandResult> ConfigurePppoeAsync(PppoeRequest request, bool apply, CancellationToken ct = default);

    /// <summary>
    /// Cria uma interface VLAN (802.1Q) sobre uma interface física, com IP via
    /// DHCP ou estático. No Linux usa <c>nmcli con add type vlan</c>. No Windows
    /// não é possível via <c>netsh</c> (depende do driver/fabricante da placa),
    /// então apenas orienta.
    /// </summary>
    Task<NetworkCommandResult> ConfigureVlanAsync(VlanRequest request, bool apply, CancellationToken ct = default);
}
