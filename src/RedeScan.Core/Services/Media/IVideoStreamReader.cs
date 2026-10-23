namespace RedeScan.Core.Services.Media;

/// <summary>Parâmetros de decodificação de um stream de vídeo.</summary>
public sealed record VideoStreamOptions
{
    /// <summary>Largura de saída dos quadros (px).</summary>
    public int Width { get; init; } = 640;

    /// <summary>Altura de saída dos quadros (px).</summary>
    public int Height { get; init; } = 360;

    /// <summary>Quadros por segundo entregues (limita CPU em grades com várias câmeras).</summary>
    public int Fps { get; init; } = 12;

    /// <summary>Transporte RTSP: "auto" (TCP e, se falhar, UDP), "tcp" ou "udp".</summary>
    public string RtspTransport { get; init; } = "auto";

    /// <summary>Tempo máximo sem receber quadros antes de reconectar.</summary>
    public TimeSpan StallTimeout { get; init; } = TimeSpan.FromSeconds(15);

    /// <summary>Espera entre tentativas de reconexão.</summary>
    public TimeSpan ReconnectDelay { get; init; } = TimeSpan.FromSeconds(3);
}

/// <summary>Estado de um leitor de stream.</summary>
public enum VideoStreamState
{
    Stopped,
    Connecting,
    Playing,
    Reconnecting,
    Failed
}

/// <summary>
/// Um quadro decodificado em BGRA 32 bits. O buffer é reutilizado entre
/// quadros: copie os dados dentro do handler do evento.
/// </summary>
public sealed class VideoFrame
{
    public VideoFrame(byte[] data, int width, int height)
    {
        Data = data;
        Width = width;
        Height = height;
    }

    public byte[] Data { get; }
    public int Width { get; }
    public int Height { get; }
    public int Stride => Width * 4;
}

/// <summary>
/// Decodifica um stream (RTSP/HTTP/arquivo) em quadros BGRA. Reconecta
/// sozinho enquanto estiver ativo.
/// </summary>
public interface IVideoStreamReader : IDisposable
{
    /// <summary>Disparado (em thread de background) a cada quadro decodificado.</summary>
    event Action<VideoFrame>? FrameReady;

    /// <summary>Disparado quando o estado muda, com uma mensagem amigável.</summary>
    event Action<VideoStreamState, string>? StateChanged;

    VideoStreamState State { get; }

    /// <summary>Inicia a leitura em background. Chamadas repetidas reiniciam o stream.</summary>
    void Start(string url, VideoStreamOptions? options = null);

    /// <summary>Para a leitura e encerra o processo decodificador.</summary>
    void Stop();
}

/// <summary>Cria leitores de stream e captura quadros únicos (prévias).</summary>
public interface IVideoService
{
    /// <summary>True se o decodificador (ffmpeg) estiver disponível.</summary>
    bool IsAvailable { get; }

    /// <summary>Cria um novo leitor de stream.</summary>
    IVideoStreamReader CreateReader();

    /// <summary>
    /// Captura um único quadro do stream como JPEG (para miniaturas). Devolve
    /// <c>null</c> se não conseguir dentro do tempo limite.
    /// </summary>
    Task<byte[]?> CaptureSnapshotAsync(string url, int width = 320, TimeSpan? timeout = null, CancellationToken ct = default);

    /// <summary>Último erro amigável da captura de prévia (diagnóstico).</summary>
    string? LastSnapshotError { get; }
}
