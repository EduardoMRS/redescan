using System.Text.Json.Serialization;

namespace RedeScan.Core.Models;

// ── Resposta da rota /releases/{projeto}/latest (deveduardomrs.pro) ──────────

/// <summary>
/// Manifesto de release retornado pela rota
/// <c>https://deveduardomrs.pro/releases/redescan/latest</c>.
/// Espelha os dados do GitHub Releases API com cache servidor.
/// </summary>
public sealed class GithubReleaseManifest
{
    [JsonPropertyName("project")]      public string Project     { get; set; } = string.Empty;
    [JsonPropertyName("owner")]        public string Owner       { get; set; } = string.Empty;
    [JsonPropertyName("version")]      public string Version     { get; set; } = "0.0.0";
    [JsonPropertyName("name")]         public string? Name       { get; set; }
    [JsonPropertyName("prerelease")]   public bool Prerelease    { get; set; }
    [JsonPropertyName("draft")]        public bool Draft         { get; set; }
    [JsonPropertyName("published_at")] public string? PublishedAt { get; set; }
    [JsonPropertyName("release_url")]  public string? ReleaseUrl { get; set; }
    [JsonPropertyName("body")]         public string? Body       { get; set; }
    [JsonPropertyName("assets")]       public List<GithubReleaseAsset> Assets { get; set; } = [];
}

/// <summary>Um asset (arquivo) de um release do GitHub.</summary>
public sealed class GithubReleaseAsset
{
    [JsonPropertyName("name")]         public string Name        { get; set; } = string.Empty;
    [JsonPropertyName("size")]         public long Size          { get; set; }
    [JsonPropertyName("content_type")] public string ContentType { get; set; } = string.Empty;
    [JsonPropertyName("download_url")] public string DownloadUrl { get; set; } = string.Empty;
    [JsonPropertyName("downloads")]    public int Downloads      { get; set; }
}

// ── Resultado interno da verificação ─────────────────────────────────────────

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
