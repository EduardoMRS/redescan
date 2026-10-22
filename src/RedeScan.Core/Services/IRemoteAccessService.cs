namespace RedeScan.Core.Services;

/// <summary>Resultado de uma tentativa de abrir um cliente de acesso remoto.</summary>
public sealed record RemoteAccessResult
{
    public bool Launched { get; init; }
    public string Command { get; init; } = string.Empty;
    public string? Error { get; init; }
}

/// <summary>
/// Abre clientes de acesso remoto do sistema (SSH, Telnet e Winbox) para um
/// dispositivo da rede. Não embute terminal/cliente próprio — delega ao cliente
/// já instalado no SO, o que é leve e multiplataforma.
/// </summary>
public interface IRemoteAccessService
{
    /// <summary>Abre uma sessão SSH (<c>ssh user@host -p porta</c>) num terminal do sistema.</summary>
    RemoteAccessResult OpenSsh(string host, int port = 22, string? username = null);

    /// <summary>Abre uma sessão Telnet (<c>telnet host porta</c>) num terminal do sistema.</summary>
    RemoteAccessResult OpenTelnet(string host, int port = 23);

    /// <summary>
    /// Abre o Winbox apontando para o host, que pode ser um IP ou um MAC
    /// (o Winbox conecta por MAC em camada 2). Requer o Winbox instalado/no PATH
    /// (no Linux, normalmente via Wine).
    /// </summary>
    RemoteAccessResult OpenWinbox(string hostOrMac);

    /// <summary>True se o Winbox foi localizado no sistema (PATH/locais comuns).</summary>
    bool IsWinboxAvailable { get; }
}
