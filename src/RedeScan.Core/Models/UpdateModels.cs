using System.Text.Json.Serialization;

namespace RedeScan.Core.Models;

/// <summary>
/// Manifesto de atualização publicado no servidor (ex.:
/// https://deveduardomrs.pro/d/redescan/release/latest.json).
/// </summary>
public sealed class UpdateManifest
{
    /// <summary>Versão mais recente disponível, ex.: "1.2.0".</summary>
    [JsonPropertyName("version")]
    public string Version { get; set; } = "0.0.0";

    /// <summary>Notas da versão (opcional).</summary>
    [JsonPropertyName("notes")]
    public string? Notes { get; set; }

    /// <summary>Data de publicação (opcional, ISO 8601).</summary>
    [JsonPropertyName("published")]
    public string? Published { get; set; }

    /// <summary>
    /// Se true, a atualização é obrigatória (a UI pode insistir). Opcional.
    /// </summary>
    [JsonPropertyName("mandatory")]
    public bool Mandatory { get; set; }

    /// <summary>Links de download por plataforma/variante.</summary>
    [JsonPropertyName("downloads")]
    public UpdateDownloads Downloads { get; set; } = new();
}

/// <summary>URLs de download por alvo. Preencha o que fizer sentido.</summary>
public sealed class UpdateDownloads
{
    [JsonPropertyName("windows")] public string? Windows { get; set; }
    [JsonPropertyName("windowsFull")] public string? WindowsFull { get; set; }
    [JsonPropertyName("linux")] public string? Linux { get; set; }         // AppImage
    [JsonPropertyName("linuxFullUbuntu")] public string? LinuxFullUbuntu { get; set; }
    [JsonPropertyName("linuxFullFedora")] public string? LinuxFullFedora { get; set; }
    [JsonPropertyName("android")] public string? Android { get; set; }     // APK
}

/// <summary>Resultado da verificação de atualização.</summary>
public sealed record UpdateCheckResult
{
    public bool UpdateAvailable { get; init; }
    public string CurrentVersion { get; init; } = string.Empty;
    public string LatestVersion { get; init; } = string.Empty;
    public string? Notes { get; init; }
    public bool Mandatory { get; init; }

    /// <summary>URL de download adequada à plataforma atual (pode ser null).</summary>
    public string? DownloadUrl { get; init; }

    /// <summary>Preenchido quando a checagem falhou (rede, etc.).</summary>
    public string? Error { get; init; }
}
