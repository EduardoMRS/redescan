using RedeScan.Core.Models;

namespace RedeScan.Core.Services;

/// <summary>
/// Reporta atividade da instalação para o servidor de licenças do portfólio,
/// via POST em <c>/page/{projeto}/license</c>. Envia ip, versão, MAC e (quando
/// configurado) o código de licença. É usado como "heartbeat": idealmente
/// chamado sempre que a aplicação é aberta.
/// </summary>
public interface ILicenseService
{
    /// <summary>Versão atualmente instalada (lida do assembly).</summary>
    string CurrentVersion { get; }

    /// <summary>
    /// Envia um heartbeat de atividade. Nunca lança: em caso de falha de rede,
    /// devolve um resultado com <see cref="LicenseHeartbeatResult.Error"/>.
    /// </summary>
    /// <param name="licenseCode">Código de licença opcional. Quando informado, o
    /// servidor responde 201; quando vazio, responde 200.</param>
    Task<LicenseHeartbeatResult> ReportActivityAsync(string? licenseCode = null, CancellationToken ct = default);
}
