namespace RedeScan.Core.Services;

/// <summary>
/// Resolve o endereço MAC de um IP consultando a tabela ARP do sistema
/// operacional. Útil para identificar o fabricante (OUI) de um host na LAN.
/// </summary>
public interface IArpService
{
    /// <summary>
    /// Retorna o MAC (formato "AA:BB:CC:DD:EE:FF") de um IP, ou <c>null</c> se
    /// não estiver na tabela ARP. A operação é best-effort e multiplataforma.
    /// </summary>
    Task<string?> GetMacAddressAsync(string ipAddress, CancellationToken ct = default);
}
