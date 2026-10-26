namespace RedeScan.Core.Models;

/// <summary>
/// Resultado do envio de um heartbeat de atividade para o servidor de licenças.
/// Nunca representa falha "dura": o relatório é best-effort e não deve impedir o
/// app de abrir.
/// </summary>
public sealed class LicenseHeartbeatResult
{
    /// <summary>Indica se a requisição chegou ao servidor e obteve 2xx.</summary>
    public bool Success { get; init; }

    /// <summary>Código HTTP retornado (201 com licença, 200 sem licença).</summary>
    public int StatusCode { get; init; }

    /// <summary>Se verdadeiro, o servidor reconheceu um código de licença (201).</summary>
    public bool LicenseAccepted { get; init; }

    /// <summary>Mensagem de erro quando o envio falhou (rede, timeout, etc.).</summary>
    public string? Error { get; init; }
}
