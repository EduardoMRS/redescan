namespace RedeScan.Core.Services;

/// <summary>Resultado de uma tentativa de abrir um cliente de acesso remoto.</summary>
public sealed record RemoteAccessResult
{
    public bool Launched { get; init; }
    public string Command { get; init; } = string.Empty;
    public string? Error { get; init; }
}

/// <summary>
/// Abre clientes de acesso remoto do sistema (SSH, Telnet e WinBox) para um
/// dispositivo da rede. SSH/Telnet abrem num terminal do sistema; o WinBox é
/// baixado automaticamente (versão oficial MikroTik) quando não existir.
/// </summary>
public interface IRemoteAccessService
{
    /// <summary>Abre uma sessão SSH (<c>ssh user@host -p porta</c>) num terminal do sistema.</summary>
    RemoteAccessResult OpenSsh(string host, int port = 22, string? username = null);

    /// <summary>Abre uma sessão Telnet (<c>telnet host porta</c>) num terminal do sistema.</summary>
    RemoteAccessResult OpenTelnet(string host, int port = 23);

    /// <summary>
    /// Abre o WinBox apontando para o host (IP ou MAC — o WinBox conecta por MAC
    /// em camada 2). Se o WinBox não estiver instalado, baixa a versão oficial
    /// (com verificação SHA-256) para a pasta de ferramentas do usuário.
    /// </summary>
    Task<RemoteAccessResult> OpenWinboxAsync(
        string hostOrMac,
        string? username = null,
        IProgress<ToolDownloadProgress>? progress = null,
        CancellationToken ct = default);

    /// <summary>True se o WinBox já está disponível localmente (sem precisar baixar).</summary>
    bool IsWinboxAvailable { get; }
}
