namespace RedeScan.Core.Services;

/// <summary>Envia o "magic packet" de Wake-on-LAN para ligar um dispositivo pelo MAC.</summary>
public interface IWakeOnLanService
{
    /// <summary>
    /// Envia o magic packet para o <paramref name="macAddress"/> (formatos
    /// "AA:BB:CC:DD:EE:FF", "AA-BB-..." ou "AABBCC..."). Opcionalmente direciona
    /// o broadcast para <paramref name="broadcastAddress"/>. Retorna true se enviado.
    /// </summary>
    Task<bool> WakeAsync(string macAddress, string? broadcastAddress = null, CancellationToken ct = default);
}
