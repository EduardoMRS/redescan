namespace RedeScan.Core.Models;

/// <summary>
/// Estrutura do arquivo <c>rtspList.json</c>, idêntica à do CentralMonitor:
/// um objeto com a lista <c>Streams</c>. Permite importar/exportar os mesmos
/// arquivos entre os dois aplicativos.
/// </summary>
public sealed class RtspListFile
{
    public List<RtspCameraConfig> Streams { get; set; } = new();
}
