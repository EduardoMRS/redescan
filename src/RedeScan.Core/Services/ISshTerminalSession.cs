namespace RedeScan.Core.Services;

/// <summary>
/// Sessão de terminal SSH interativa (shell), utilizável em todas as
/// plataformas (desktop e Android) por ser 100% gerenciada. Não é um emulador
/// VT100 completo — é um shell de linha/stream adequado para CLI de roteadores,
/// switches e servidores.
/// </summary>
public interface ISshTerminalSession : IDisposable
{
    /// <summary>Disparado quando chega saída do servidor (texto).</summary>
    event Action<string>? OutputReceived;

    /// <summary>Disparado quando a sessão encerra (normal ou por erro).</summary>
    event Action<string?>? Disconnected;

    bool IsConnected { get; }

    /// <summary>Conecta e abre o shell interativo.</summary>
    Task ConnectAsync(SshConnectionInfo info, CancellationToken ct = default);

    /// <summary>Envia um texto/comando ao shell (normalmente seguido de \n).</summary>
    Task SendAsync(string data, CancellationToken ct = default);

    /// <summary>Encerra a sessão.</summary>
    void Disconnect();
}

/// <summary>Dados de conexão SSH.</summary>
public sealed class SshConnectionInfo
{
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 22;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public int TimeoutSeconds { get; set; } = 15;
}

/// <summary>Fábrica de sessões (registrada no DI) para a UI criar uma por aba/conexão.</summary>
public interface ISshTerminalFactory
{
    ISshTerminalSession Create();
}
