using System.Diagnostics;
using System.IO.Compression;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using RedeScan.Core.Diagnostics;

namespace RedeScan.Core.Services;

/// <summary>
/// Implementação de <see cref="IExternalToolService"/>. Fontes oficiais:
/// <list type="bullet">
/// <item>ffmpeg Windows: gyan.dev (release essentials, verificado por SHA-256).</item>
/// <item>ffmpeg Linux: johnvansickle.com (build estático, verificado por MD5 publicado).</item>
/// <item>WinBox 4: download.mikrotik.com (verificado por SHA-256).</item>
/// </list>
/// </summary>
public sealed partial class ExternalToolService : IExternalToolService
{
    private const string FallbackWinBoxVersion = "4.4";

    private static bool IsWindows => RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
    private static bool IsLinux => RuntimeInformation.IsOSPlatform(OSPlatform.Linux);

    private readonly HttpClient _http;
    private readonly Dictionary<ExternalTool, string?> _custom = new();
    private readonly SemaphoreSlim _downloadGate = new(1, 1);

    public ExternalToolService() : this(new HttpClient { Timeout = TimeSpan.FromMinutes(15) })
    {
    }

    public ExternalToolService(HttpClient http)
    {
        _http = http;
        if (!_http.DefaultRequestHeaders.UserAgent.Any())
        {
            _http.DefaultRequestHeaders.UserAgent.ParseAdd("RedeScan/1.0 (+https://github.com/EduardoMRS/redescan)");
        }
    }

    public void SetCustomPath(ExternalTool tool, string? path) => _custom[tool] = path;

    public string? Find(ExternalTool tool)
    {
        if (_custom.TryGetValue(tool, out var custom) && !string.IsNullOrWhiteSpace(custom) && File.Exists(custom))
        {
            return custom;
        }

        return tool switch
        {
            ExternalTool.Ffmpeg => FindFfmpeg(),
            ExternalTool.WinBox => FindWinBox(),
            _ => null
        };
    }

    public async Task<string> EnsureAsync(
        ExternalTool tool,
        IProgress<ToolDownloadProgress>? progress = null,
        CancellationToken ct = default)
    {
        var existing = Find(tool);
        if (existing is not null)
        {
            return existing;
        }

        await _downloadGate.WaitAsync(ct);
        try
        {
            // Outro chamador pode ter baixado enquanto esperávamos.
            existing = Find(tool);
            if (existing is not null)
            {
                return existing;
            }

            var path = tool switch
            {
                ExternalTool.Ffmpeg => await DownloadFfmpegAsync(progress, ct),
                ExternalTool.WinBox => await DownloadWinBoxAsync(progress, ct),
                _ => throw new NotSupportedException(tool.ToString())
            };

            AppLog.Info($"{tool} instalado em {path}");
            return path;
        }
        finally
        {
            _downloadGate.Release();
        }
    }

    // ================================================================ ffmpeg
    private static string FfmpegExeName => IsWindows ? "ffmpeg.exe" : "ffmpeg";

    private static string? FindFfmpeg()
    {
        var candidates = new List<string>
        {
            // Instalador Full (Windows) coloca em {app}\ffmpeg\ffmpeg.exe.
            Path.Combine(AppPaths.AppDir, "ffmpeg", FfmpegExeName),
            Path.Combine(AppPaths.AppDir, FfmpegExeName),
            Path.Combine(AppPaths.ToolsDir, "ffmpeg", FfmpegExeName)
        };

        var onPath = FindOnPath(FfmpegExeName);
        if (onPath is not null)
        {
            candidates.Add(onPath);
        }

        if (!IsWindows)
        {
            candidates.Add("/usr/bin/ffmpeg");
            candidates.Add("/usr/local/bin/ffmpeg");
            candidates.Add("/snap/bin/ffmpeg");
        }

        return candidates.FirstOrDefault(File.Exists);
    }

    private async Task<string> DownloadFfmpegAsync(IProgress<ToolDownloadProgress>? progress, CancellationToken ct)
    {
        var targetDir = Path.Combine(AppPaths.ToolsDir, "ffmpeg");
        var work = Path.Combine(AppPaths.ToolsDir, ".download-ffmpeg");
        RecreateDir(work);

        try
        {
            if (IsWindows)
            {
                const string url = "https://www.gyan.dev/ffmpeg/builds/ffmpeg-release-essentials.zip";
                var archive = Path.Combine(work, "ffmpeg.zip");
                await DownloadFileAsync(ExternalTool.Ffmpeg, url, archive, progress, ct);
                var expected = (await _http.GetStringAsync(url + ".sha256", ct)).Trim().Split(' ')[0];
                VerifyHash(archive, expected, SHA256.Create());

                progress?.Report(new(ExternalTool.Ffmpeg, -1, "Extraindo ffmpeg…"));
                var extracted = Path.Combine(work, "x");
                ZipFile.ExtractToDirectory(archive, extracted);
                var exe = Directory.EnumerateFiles(extracted, "ffmpeg.exe", SearchOption.AllDirectories).First();
                var probe = Path.Combine(Path.GetDirectoryName(exe)!, "ffprobe.exe");
                RecreateDir(targetDir);
                File.Copy(exe, Path.Combine(targetDir, "ffmpeg.exe"), true);
                if (File.Exists(probe))
                {
                    File.Copy(probe, Path.Combine(targetDir, "ffprobe.exe"), true);
                }
            }
            else if (IsLinux && RuntimeInformation.OSArchitecture == Architecture.X64)
            {
                const string url = "https://johnvansickle.com/ffmpeg/releases/ffmpeg-release-amd64-static.tar.xz";
                var archive = Path.Combine(work, "ffmpeg.tar.xz");
                await DownloadFileAsync(ExternalTool.Ffmpeg, url, archive, progress, ct);
                var expected = (await _http.GetStringAsync(url + ".md5", ct)).Trim().Split(' ')[0];
                VerifyHash(archive, expected, MD5.Create());

                progress?.Report(new(ExternalTool.Ffmpeg, -1, "Extraindo ffmpeg…"));
                var extracted = Path.Combine(work, "x");
                Directory.CreateDirectory(extracted);
                // O .NET não descompacta xz nativamente; o tar do sistema sim.
                await RunAsync("tar", new[] { "-xJf", archive, "-C", extracted }, ct);
                var exe = Directory.EnumerateFiles(extracted, "ffmpeg", SearchOption.AllDirectories).First();
                RecreateDir(targetDir);
                var dest = Path.Combine(targetDir, "ffmpeg");
                File.Copy(exe, dest, true);
                MakeExecutable(dest);
            }
            else
            {
                throw new PlatformNotSupportedException(
                    "Download automático do ffmpeg disponível para Windows e Linux x64. " +
                    "Instale o ffmpeg pelo gerenciador de pacotes do sistema.");
            }

            return Path.Combine(targetDir, FfmpegExeName);
        }
        finally
        {
            TryDeleteDir(work);
        }
    }

    // ================================================================ WinBox
    private static string WinBoxExeName => IsWindows ? "WinBox.exe" : "WinBox";

    private static string? FindWinBox()
    {
        var candidates = new List<string>
        {
            Path.Combine(AppPaths.ToolsDir, "winbox", WinBoxExeName)
        };

        foreach (var name in new[] { "WinBox", "winbox", "winbox64", "WinBox.exe", "winbox.exe", "winbox64.exe" })
        {
            var p = FindOnPath(name);
            if (p is not null)
            {
                candidates.Add(p);
            }
        }

        if (IsWindows)
        {
            var pf = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            var pfx86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
            var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            candidates.Add(Path.Combine(pf, "WinBox", "WinBox.exe"));
            candidates.Add(Path.Combine(pf, "winbox.exe"));
            candidates.Add(Path.Combine(pfx86, "winbox.exe"));
            candidates.Add(Path.Combine(local, "Programs", "WinBox", "WinBox.exe"));
        }
        else
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            candidates.Add(Path.Combine(home, ".local", "bin", "WinBox"));
            candidates.Add(Path.Combine(home, ".local", "bin", "winbox"));
            candidates.Add("/opt/winbox/WinBox");
            candidates.Add("/usr/local/bin/winbox");
        }

        return candidates.FirstOrDefault(File.Exists);
    }

    private async Task<string> DownloadWinBoxAsync(IProgress<ToolDownloadProgress>? progress, CancellationToken ct)
    {
        string asset;
        if (IsWindows)
        {
            asset = RuntimeInformation.OSArchitecture == Architecture.Arm64
                ? "WinBox_Windows_arm64.zip"
                : "WinBox_Windows.zip";
        }
        else if (IsLinux && RuntimeInformation.OSArchitecture == Architecture.X64)
        {
            asset = "WinBox_Linux.zip";
        }
        else
        {
            throw new PlatformNotSupportedException("WinBox automático disponível para Windows e Linux x64.");
        }

        var version = await ResolveLatestWinBoxVersionAsync(ct);
        var url = $"https://download.mikrotik.com/routeros/winbox/{version}/{asset}";
        var targetDir = Path.Combine(AppPaths.ToolsDir, "winbox");
        var work = Path.Combine(AppPaths.ToolsDir, ".download-winbox");
        RecreateDir(work);

        try
        {
            var archive = Path.Combine(work, asset);
            await DownloadFileAsync(ExternalTool.WinBox, url, archive, progress, ct);
            var expected = (await _http.GetStringAsync(url + ".sha256", ct)).Trim().Split(' ')[0];
            VerifyHash(archive, expected, SHA256.Create());

            progress?.Report(new(ExternalTool.WinBox, -1, "Extraindo WinBox…"));
            RecreateDir(targetDir);
            ZipFile.ExtractToDirectory(archive, targetDir, overwriteFiles: true);

            var exe = Directory.EnumerateFiles(targetDir, WinBoxExeName, SearchOption.AllDirectories).FirstOrDefault()
                      ?? throw new FileNotFoundException("WinBox não encontrado dentro do pacote baixado.");
            if (!IsWindows)
            {
                MakeExecutable(exe);
            }

            return exe;
        }
        finally
        {
            TryDeleteDir(work);
        }
    }

    /// <summary>Lê a versão mais recente na página oficial; cai para uma versão conhecida.</summary>
    private async Task<string> ResolveLatestWinBoxVersionAsync(CancellationToken ct)
    {
        try
        {
            var html = await _http.GetStringAsync("https://mikrotik.com/download/winbox", ct);
            var versions = WinBoxVersionRegex().Matches(html)
                .Select(m => m.Groups[1].Value)
                .Distinct()
                .OrderByDescending(v => Version.TryParse(v, out var parsed) ? parsed : new Version(0, 0))
                .ToList();
            if (versions.Count > 0)
            {
                return versions[0];
            }
        }
        catch (Exception ex)
        {
            AppLog.Warn($"Não foi possível consultar a versão do WinBox: {ex.Message}");
        }

        return FallbackWinBoxVersion;
    }

    [GeneratedRegex(@"download\.mikrotik\.com/routeros/winbox/(\d+(?:\.\d+)+)/WinBox_")]
    private static partial Regex WinBoxVersionRegex();

    // ================================================================ infra
    private async Task DownloadFileAsync(
        ExternalTool tool, string url, string destination,
        IProgress<ToolDownloadProgress>? progress, CancellationToken ct)
    {
        AppLog.Info($"Baixando {tool}: {url}");
        using var response = await _http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();
        var total = response.Content.Headers.ContentLength;

        await using var source = await response.Content.ReadAsStreamAsync(ct);
        await using var target = File.Create(destination);
        var buffer = new byte[81920];
        long read = 0;
        var lastPercent = -2;
        int n;
        while ((n = await source.ReadAsync(buffer, ct)) > 0)
        {
            await target.WriteAsync(buffer.AsMemory(0, n), ct);
            read += n;
            var percent = total is > 0 ? (int)(read * 100 / total.Value) : -1;
            if (percent != lastPercent)
            {
                lastPercent = percent;
                var mb = read / 1024d / 1024d;
                progress?.Report(new(tool, percent, $"Baixando {tool}… {mb:F1} MB" + (percent >= 0 ? $" ({percent}%)" : string.Empty)));
            }
        }
    }

    private static void VerifyHash(string file, string expectedHex, HashAlgorithm algorithm)
    {
        using (algorithm)
        using (var stream = File.OpenRead(file))
        {
            var actual = Convert.ToHexString(algorithm.ComputeHash(stream));
            if (!actual.Equals(expectedHex.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException(
                    $"Soma de verificação não confere (esperado {expectedHex}, obtido {actual}). Download descartado.");
            }
        }
    }

    private static async Task RunAsync(string file, IEnumerable<string> args, CancellationToken ct)
    {
        var psi = new ProcessStartInfo(file)
        {
            UseShellExecute = false,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            CreateNoWindow = true
        };
        foreach (var a in args)
        {
            psi.ArgumentList.Add(a);
        }

        using var p = Process.Start(psi) ?? throw new InvalidOperationException($"Falha ao iniciar {file}.");
        var err = await p.StandardError.ReadToEndAsync(ct);
        await p.WaitForExitAsync(ct);
        if (p.ExitCode != 0)
        {
            throw new InvalidOperationException($"{file} terminou com código {p.ExitCode}: {err}");
        }
    }

    private static void MakeExecutable(string path)
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        File.SetUnixFileMode(path,
            UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
            UnixFileMode.GroupRead | UnixFileMode.GroupExecute |
            UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
    }

    internal static string? FindOnPath(string exe)
    {
        var pathVar = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrEmpty(pathVar))
        {
            return null;
        }

        foreach (var dir in pathVar.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            try
            {
                var full = Path.Combine(dir, exe);
                if (File.Exists(full))
                {
                    return full;
                }
            }
            catch
            {
                // entrada inválida no PATH
            }
        }

        return null;
    }

    private static void RecreateDir(string dir)
    {
        TryDeleteDir(dir);
        Directory.CreateDirectory(dir);
    }

    private static void TryDeleteDir(string dir)
    {
        try
        {
            if (Directory.Exists(dir))
            {
                Directory.Delete(dir, recursive: true);
            }
        }
        catch
        {
            // arquivo em uso — limpa na próxima vez
        }
    }
}
