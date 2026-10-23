namespace RedeScan.Core.Services;

/// <summary>Ferramentas externas que o RedeScan sabe localizar e baixar.</summary>
public enum ExternalTool
{
    /// <summary>ffmpeg — decodifica os streams RTSP das câmeras e gera prévias.</summary>
    Ffmpeg,

    /// <summary>WinBox 4 (MikroTik) — cliente gráfico de RouterOS.</summary>
    WinBox
}

/// <summary>Progresso de um download (0–100, ou -1 quando o tamanho é desconhecido).</summary>
public sealed record ToolDownloadProgress(ExternalTool Tool, int Percent, string Message);

/// <summary>
/// Localiza ferramentas externas (pasta do app, pasta de ferramentas do
/// usuário, PATH, locais comuns) e, quando ausentes, baixa a versão oficial,
/// verifica a soma de verificação publicada e a extrai em
/// <see cref="Diagnostics.AppPaths.ToolsDir"/> — sem exigir administrador.
/// </summary>
public interface IExternalToolService
{
    /// <summary>Caminho do executável, ou <c>null</c> se não estiver disponível.</summary>
    string? Find(ExternalTool tool);

    /// <summary>
    /// Garante que a ferramenta exista: devolve o caminho já disponível ou
    /// baixa/instala e devolve o novo caminho. Lança exceção em caso de falha.
    /// </summary>
    Task<string> EnsureAsync(
        ExternalTool tool,
        IProgress<ToolDownloadProgress>? progress = null,
        CancellationToken ct = default);

    /// <summary>Caminho definido manualmente pelo usuário (sobrepõe a busca).</summary>
    void SetCustomPath(ExternalTool tool, string? path);
}
