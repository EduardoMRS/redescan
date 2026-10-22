using RedeScan.Core.Models;

namespace RedeScan.Core.Services;

/// <summary>
/// Faz o scan aprofundado de um host: identifica serviços (banner grabbing),
/// estima o SO, resolve nome, MAC e fabricante.
/// </summary>
public interface IHostInspector
{
    /// <summary>
    /// Inspeciona <paramref name="ipAddress"/> varrendo as portas informadas
    /// (ou as conhecidas, se null), capturando banners e montando o perfil.
    /// </summary>
    Task<HostDetails> InspectAsync(
        string ipAddress,
        IEnumerable<int>? ports = null,
        int timeoutMs = 800,
        CancellationToken ct = default);
}
