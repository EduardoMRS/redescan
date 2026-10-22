using System.Text.Json;
using RedeScan.Core.Models;

namespace RedeScan.Core.Services;

/// <summary>
/// Implementação de <see cref="IRtspListImporter"/>. Desserializa de forma
/// case-insensitive, aceitando tanto <c>Streams</c> quanto <c>streams</c>
/// (o CentralMonitor grava o arquivo vazio como <c>{"streams": []}</c>).
/// </summary>
public sealed class RtspListImporter : IRtspListImporter
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    public async Task<IReadOnlyList<RtspCameraConfig>> ImportAsync(string filePath, CancellationToken ct = default)
    {
        if (!File.Exists(filePath))
        {
            return Array.Empty<RtspCameraConfig>();
        }

        await using var stream = File.OpenRead(filePath);
        var file = await JsonSerializer.DeserializeAsync<RtspListFile>(stream, Options, ct);
        var cameras = file?.Streams ?? new List<RtspCameraConfig>();

        // Normaliza: garante Id e grupo para itens vindos do CentralMonitor
        // (que não têm Id nem IsEnabled).
        var order = 0;
        foreach (var cam in cameras)
        {
            if (string.IsNullOrWhiteSpace(cam.Id))
            {
                cam.Id = Guid.NewGuid().ToString("N");
            }
            if (string.IsNullOrWhiteSpace(cam.Group))
            {
                cam.Group = "Geral";
            }
            cam.DisplayOrder = order++;
        }

        return cameras;
    }

    public async Task ExportAsync(string filePath, IEnumerable<RtspCameraConfig> cameras, CancellationToken ct = default)
    {
        var dir = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrWhiteSpace(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        var file = new RtspListFile { Streams = cameras.ToList() };
        await using var stream = File.Create(filePath);
        await JsonSerializer.SerializeAsync(stream, file, Options, ct);
    }
}
