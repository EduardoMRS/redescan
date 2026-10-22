using RedeScan.Core.Models;

namespace RedeScan.Core.Services;

/// <summary>
/// Importa/exporta listas de câmeras RTSP no formato <c>rtspList.json</c> do
/// CentralMonitor (<c>{ "Streams": [ { Name, Url, Location, Group } ] }</c>).
/// </summary>
public interface IRtspListImporter
{
    /// <summary>Lê um arquivo rtspList.json e devolve as câmeras.</summary>
    Task<IReadOnlyList<RtspCameraConfig>> ImportAsync(string filePath, CancellationToken ct = default);

    /// <summary>Grava as câmeras em um arquivo no formato rtspList.json.</summary>
    Task ExportAsync(string filePath, IEnumerable<RtspCameraConfig> cameras, CancellationToken ct = default);
}
