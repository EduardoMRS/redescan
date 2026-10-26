using RedeScan.Core.Models;

namespace RedeScan.Core.Services;

/// <summary>
/// Verifica se há uma versão mais recente do RedeScan publicada no servidor de
/// releases, consultando a rota <c>/releases/{projeto}/latest</c> do portfólio
/// e comparando com a versão instalada no assembly.
/// </summary>
public interface IUpdateService
{
    /// <summary>Versão atualmente instalada (lida do assembly).</summary>
    string CurrentVersion { get; }

    /// <summary>
    /// Busca o manifesto na URL informada e compara com a versão instalada.
    /// Nunca lança: em caso de falha, devolve um resultado com <c>Error</c>.
    /// </summary>
    Task<UpdateCheckResult> CheckAsync(string manifestUrl, CancellationToken ct = default);
}
