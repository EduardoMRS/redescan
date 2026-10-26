using System.Net.Http.Json;
using System.Reflection;
using System.Runtime.InteropServices;
using RedeScan.Core.Diagnostics;
using RedeScan.Core.Models;

namespace RedeScan.Core.Services;

/// <summary>
/// Implementação de <see cref="IUpdateService"/>. Lê a versão instalada do
/// assembly (<see cref="AssemblyInformationalVersionAttribute"/>) e consulta a
/// rota <c>/releases/{projeto}/latest</c> do portfólio para comparar.
///
/// A seleção do link de download considera a plataforma atual e, quando possível,
/// prefere o instalador "portable" (menor); o "full" fica como fallback.
/// </summary>
public sealed class UpdateService : IUpdateService
{
    private readonly HttpClient _http;

    public UpdateService(HttpClient? http = null)
    {
        // Timeout generoso: na primeira checagem após expirar o cache, o servidor
        // ainda consulta a API do GitHub (até ~8s), então 10s era curto demais e
        // fazia a verificação falhar silenciosamente. 20s dá folga.
        _http = http ?? new HttpClient { Timeout = TimeSpan.FromSeconds(20) };

        // Alguns CDNs/limitadores estrangulam requisições sem User-Agent.
        if (!_http.DefaultRequestHeaders.UserAgent.Any())
        {
            _http.DefaultRequestHeaders.UserAgent.ParseAdd(
                $"RedeScan/{CurrentVersion} (+https://github.com/EduardoMRS/redescan)");
        }
    }

    public string CurrentVersion { get; } = ReadCurrentVersion();

    public async Task<UpdateCheckResult> CheckAsync(string manifestUrl, CancellationToken ct = default)
    {
        try
        {
            AppLog.Info($"Verificando atualizações em {manifestUrl} (versão atual: {CurrentVersion}).");

            var manifest = await _http.GetFromJsonAsync<GithubReleaseManifest>(manifestUrl, ct);
            if (manifest is null)
            {
                AppLog.Warn("Verificação de atualização: manifesto vazio ou inválido.");
                return Fail("Manifesto vazio ou inválido.");
            }

            // Ignora pre-releases e drafts
            if (manifest.Draft || manifest.Prerelease)
            {
                AppLog.Info($"Verificação de atualização: release {manifest.Version} é draft/prerelease — ignorado.");
                return new UpdateCheckResult
                {
                    UpdateAvailable = false,
                    CurrentVersion  = CurrentVersion,
                    LatestVersion   = manifest.Version,
                };
            }

            var current  = ParseVersion(CurrentVersion);
            var latest   = ParseVersion(manifest.Version);
            var isNewer  = latest > current;

            AppLog.Info($"Verificação de atualização: instalada={current}, publicada={latest}, atualizar={isNewer}.");

            return new UpdateCheckResult
            {
                UpdateAvailable = isNewer,
                CurrentVersion  = CurrentVersion,
                LatestVersion   = manifest.Version,
                Notes           = manifest.Body,
                Mandatory       = false,
                DownloadUrl     = isNewer ? PickDownload(manifest.Assets) : null,
            };
        }
        catch (Exception ex)
        {
            AppLog.Warn("Falha ao verificar atualizações: " + ex.Message);
            return Fail(ex.Message);
        }
    }

    private UpdateCheckResult Fail(string error) => new()
    {
        UpdateAvailable = false,
        CurrentVersion  = CurrentVersion,
        LatestVersion   = CurrentVersion,
        Error           = error,
    };

    /// <summary>
    /// Escolhe o asset mais adequado à plataforma atual.
    ///
    /// Convenção de nomes de arquivo dos releases do RedeScan:
    ///   RedeScan-Windows-x64.exe          → Windows portable (sem runtime incluso)
    ///   RedeScan-Full-Windows-x64.exe     → Windows Full (runtime .NET incluso)
    ///   RedeScan-Linux-x86_64.AppImage    → Linux portable (requer .NET instalado)
    ///   RedeScan-Full-Linux-x64.deb       → Linux Full — Debian/Ubuntu (runtime incluso)
    ///   RedeScan-Full-Linux-x86_64.rpm    → Linux Full — Fedora/Red Hat (runtime incluso)
    ///   RedeScan-Android.apk              → Android
    ///
    /// Estratégia:
    ///   • Windows → portable primeiro, full como fallback
    ///   • Linux   → AppImage portable primeiro; se não disponível, detecta distro:
    ///               Debian/Ubuntu → .deb | Fedora/RHEL → .rpm | fallback: .deb ou .rpm
    ///   • Android → .apk
    /// </summary>
    private static string? PickDownload(List<GithubReleaseAsset> assets)
    {
        string? Find(Func<string, bool> predicate) =>
            assets.FirstOrDefault(a => predicate(a.Name.ToLowerInvariant()))?.DownloadUrl;

        if (OperatingSystem.IsAndroid())
        {
            return Find(n => n.EndsWith(".apk"));
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            // Portable primeiro (sem "full" no nome), Full como fallback
            return Find(n => n.Contains("windows") && !n.Contains("full"))
                ?? Find(n => n.Contains("windows") && n.Contains("full"));
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            // AppImage portable é universal — prefere sempre
            var appImage = Find(n => n.EndsWith(".appimage"));
            if (appImage is not null) return appImage;

            // Sem AppImage: detecta família da distro e escolhe o pacote certo
            if (IsDebianBased())
                return Find(n => n.EndsWith(".deb")) ?? Find(n => n.EndsWith(".rpm"));

            if (IsRpmBased())
                return Find(n => n.EndsWith(".rpm")) ?? Find(n => n.EndsWith(".deb"));

            // Fallback genérico: .deb, depois .rpm
            return Find(n => n.EndsWith(".deb")) ?? Find(n => n.EndsWith(".rpm"));
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return Find(n => n.EndsWith(".dmg") || n.Contains("macos") || n.Contains("mac"));
        }

        // Último fallback: qualquer asset que não seja metadado
        return assets.FirstOrDefault(a => !a.Name.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                     ?.DownloadUrl;
    }

    /// <summary>Verifica se o sistema é baseado em Debian/Ubuntu (presença de /etc/debian_version).</summary>
    private static bool IsDebianBased() =>
        File.Exists("/etc/debian_version") || File.Exists("/etc/ubuntu_version");

    /// <summary>Verifica se o sistema é baseado em RPM (Fedora, RHEL, openSUSE, etc.).</summary>
    private static bool IsRpmBased() =>
        File.Exists("/etc/fedora-release")  ||
        File.Exists("/etc/redhat-release")  ||
        File.Exists("/etc/centos-release")  ||
        File.Exists("/etc/system-release"); // openSUSE/SLES também usa este

    private static string ReadCurrentVersion()
    {
        var asm  = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();
        var info = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        // InformationalVersion pode vir com sufixo "+commit"; corta no '+'.
        if (!string.IsNullOrWhiteSpace(info))
        {
            var plus = info.IndexOf('+');
            return plus > 0 ? info[..plus] : info;
        }
        return asm.GetName().Version?.ToString(3) ?? "0.0.0";
    }

    /// <summary>Compara versões de forma tolerante (ignora prefixo "v" e sufixos não numéricos).</summary>
    private static Version ParseVersion(string raw)
    {
        // Remove prefixo "v" comum nos tags do GitHub (ex.: "v1.0.4" → "1.0.4")
        var trimmed = raw.TrimStart('v', 'V');
        var clean   = new string(trimmed.TakeWhile(c => char.IsDigit(c) || c == '.').ToArray());
        return Version.TryParse(clean, out var v) ? v : new Version(0, 0, 0);
    }
}

