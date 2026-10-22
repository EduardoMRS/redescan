using System.Net.Http.Json;
using System.Reflection;
using System.Runtime.InteropServices;
using RedeScan.Core.Models;

namespace RedeScan.Core.Services;

/// <summary>
/// Implementação de <see cref="IUpdateService"/>. Lê a versão instalada do
/// assembly (<see cref="AssemblyInformationalVersionAttribute"/>) e baixa o
/// manifesto remoto para comparar. A seleção do link de download considera a
/// plataforma atual.
/// </summary>
public sealed class UpdateService : IUpdateService
{
    private readonly HttpClient _http;

    public UpdateService(HttpClient? http = null)
    {
        _http = http ?? new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
    }

    public string CurrentVersion { get; } = ReadCurrentVersion();

    public async Task<UpdateCheckResult> CheckAsync(string manifestUrl, CancellationToken ct = default)
    {
        try
        {
            var manifest = await _http.GetFromJsonAsync<UpdateManifest>(manifestUrl, ct);
            if (manifest is null)
            {
                return Fail("Manifesto vazio ou inválido.");
            }

            var current = ParseVersion(CurrentVersion);
            var latest = ParseVersion(manifest.Version);
            var isNewer = latest > current;

            return new UpdateCheckResult
            {
                UpdateAvailable = isNewer,
                CurrentVersion = CurrentVersion,
                LatestVersion = manifest.Version,
                Notes = manifest.Notes,
                Mandatory = manifest.Mandatory,
                DownloadUrl = isNewer ? PickDownload(manifest.Downloads) : null
            };
        }
        catch (Exception ex)
        {
            return Fail(ex.Message);
        }
    }

    private UpdateCheckResult Fail(string error) => new()
    {
        UpdateAvailable = false,
        CurrentVersion = CurrentVersion,
        LatestVersion = CurrentVersion,
        Error = error
    };

    /// <summary>Escolhe a URL de download conforme a plataforma atual.</summary>
    private static string? PickDownload(UpdateDownloads d)
    {
        if (OperatingSystem.IsAndroid())
        {
            return d.Android;
        }
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return d.Windows ?? d.WindowsFull;
        }
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return d.Linux ?? d.LinuxFullUbuntu ?? d.LinuxFullFedora;
        }
        // Fallback: primeiro link não nulo.
        return d.Windows ?? d.Linux ?? d.Android ?? d.WindowsFull ?? d.LinuxFullUbuntu ?? d.LinuxFullFedora;
    }

    private static string ReadCurrentVersion()
    {
        var asm = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();
        var info = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        // InformationalVersion pode vir com sufixo "+commit"; corta no '+'.
        if (!string.IsNullOrWhiteSpace(info))
        {
            var plus = info.IndexOf('+');
            return plus > 0 ? info[..plus] : info;
        }
        return asm.GetName().Version?.ToString(3) ?? "0.0.0";
    }

    /// <summary>Compara versões de forma tolerante (ignora sufixos não numéricos).</summary>
    private static Version ParseVersion(string raw)
    {
        var clean = new string(raw.TakeWhile(c => char.IsDigit(c) || c == '.').ToArray());
        return Version.TryParse(clean, out var v) ? v : new Version(0, 0, 0);
    }
}
