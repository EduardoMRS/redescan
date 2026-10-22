namespace RedeScan.Core.Services;

/// <summary>Evento de log do servidor TFTP (para exibir na UI).</summary>
public sealed record TftpLogEvent(DateTime Timestamp, string Message, bool IsError = false);

/// <summary>
/// Servidor TFTP (RFC 1350) embutido, útil para recuperar dispositivos em modo
/// de recuperação (roteadores/ONUs/switches "brickados" que baixam o firmware
/// via TFTP). Serve arquivos de uma pasta raiz (RRQ) e opcionalmente recebe
/// uploads (WRQ). 100% gerenciado — não depende de tftpd do sistema.
/// </summary>
public interface ITftpServer : IDisposable
{
    bool IsRunning { get; }

    /// <summary>Pasta raiz de onde os arquivos são servidos / para onde uploads vão.</summary>
    string RootDirectory { get; }

    /// <summary>Porta UDP em uso (padrão TFTP é 69, que exige privilégio).</summary>
    int Port { get; }

    event Action<TftpLogEvent>? Log;

    /// <summary>
    /// Inicia o servidor. <paramref name="port"/> 69 é o padrão (exige root/admin);
    /// use uma porta alta (ex.: 6969) para rodar sem privilégio.
    /// <paramref name="allowWrite"/> habilita o recebimento de uploads (WRQ).
    /// </summary>
    Task StartAsync(string rootDirectory, int port = 69, bool allowWrite = false, CancellationToken ct = default);

    /// <summary>Para o servidor.</summary>
    void Stop();
}
