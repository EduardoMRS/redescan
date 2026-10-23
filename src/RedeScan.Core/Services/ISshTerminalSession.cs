namespace RedeScan.Core.Services;

/// <summary>
/// Sessão de terminal interativa (SSH, Telnet ou shell local). Não é um
/// emulador VT100 completo — é um shell de linha/stream adequado para CLI de
/// roteadores, switches e servidores.
/// </summary>
public interface ITerminalSession : IDisposable
{
    /// <summary>Disparado quando chega saída (texto).</summary>
    event Action<string>? OutputReceived;

    /// <summary>Disparado quando a sessão encerra (normal ou por erro).</summary>
    event Action<string?>? Disconnected;

    bool IsConnected { get; }

    /// <summary>
    /// True quando o outro lado não ecoa o que é digitado (ex.: shell local sem
    /// pseudo-terminal): a UI deve mostrar o comando ela mesma.
    /// </summary>
    bool NeedsLocalEcho { get; }

    /// <summary>Envia texto ao shell. Use "\r" para Enter.</summary>
    Task SendAsync(string data, CancellationToken ct = default);

    /// <summary>Encerra a sessão.</summary>
    void Disconnect();
}

/// <summary>Sessão SSH (SSH.NET), utilizável em desktop e Android.</summary>
public interface ISshTerminalSession : ITerminalSession
{
    /// <summary>Conecta e abre o shell interativo.</summary>
    Task ConnectAsync(SshConnectionInfo info, CancellationToken ct = default);
}

/// <summary>Sessão Telnet (RFC 854) gerenciada — não depende do cliente do sistema.</summary>
public interface ITelnetTerminalSession : ITerminalSession
{
    Task ConnectAsync(string host, int port = 23, int timeoutSeconds = 15, CancellationToken ct = default);
}

/// <summary>Shell local deste dispositivo (bash/sh no Linux, cmd.exe no Windows).</summary>
public interface ILocalShellSession : ITerminalSession
{
    /// <summary>Descrição do shell em uso (ex.: "/bin/bash (PTY)").</summary>
    string ShellDescription { get; }

    Task StartAsync(CancellationToken ct = default);
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

/// <summary>Fábrica de sessões (registrada no DI) — uma sessão por aba/conexão.</summary>
public interface ISshTerminalFactory
{
    ISshTerminalSession Create();

    ITelnetTerminalSession CreateTelnet();

    ILocalShellSession CreateLocal();
}
