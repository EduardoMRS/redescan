using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using RedeScan.Core.Diagnostics;

namespace RedeScan.Core.Services.Media;

/// <summary>
/// Implementação de <see cref="IVideoService"/> sobre o <c>ffmpeg</c> rodando
/// como processo separado. Funciona igual em Windows e Linux, com ou sem
/// privilégio, e um travamento do decodificador nunca derruba o app.
/// </summary>
public sealed class FfmpegVideoService : IVideoService
{
    private readonly IExternalToolService _tools;

    public FfmpegVideoService(IExternalToolService tools)
    {
        _tools = tools;
    }

    public bool IsAvailable => _tools.Find(ExternalTool.Ffmpeg) is not null;

    public string? LastSnapshotError { get; private set; }

    public IVideoStreamReader CreateReader() => new FfmpegStreamReader(() => _tools.Find(ExternalTool.Ffmpeg));

    public async Task<byte[]?> CaptureSnapshotAsync(string url, int width = 320, TimeSpan? timeout = null, CancellationToken ct = default)
    {
        var ffmpeg = _tools.Find(ExternalTool.Ffmpeg);
        if (ffmpeg is null || string.IsNullOrWhiteSpace(url))
        {
            LastSnapshotError = ffmpeg is null ? "ffmpeg indisponível." : "URL vazia.";
            return null;
        }

        var limit = timeout ?? TimeSpan.FromSeconds(12);
        foreach (var transport in FfmpegArgs.TransportsFor(url, "auto"))
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(limit);

            var args = FfmpegArgs.Input(url, transport)
                .Concat(new[]
                {
                    "-an", "-sn", "-dn",
                    "-frames:v", "1",
                    "-vf", $"scale={width}:-2",
                    "-f", "image2", "-c:v", "mjpeg", "-q:v", "5",
                    "pipe:1"
                });

            using var process = FfmpegArgs.Start(ffmpeg, args);
            var stderr = process.StandardError.ReadToEndAsync(CancellationToken.None);
            try
            {
                using var ms = new MemoryStream();
                await process.StandardOutput.BaseStream.CopyToAsync(ms, cts.Token);
                await process.WaitForExitAsync(cts.Token);
                if (ms.Length > 0)
                {
                    LastSnapshotError = null;
                    return ms.ToArray();
                }

                LastSnapshotError = FfmpegArgs.Explain(await stderr);
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                LastSnapshotError = "Tempo esgotado ao conectar no stream.";
            }
            finally
            {
                FfmpegArgs.Kill(process);
            }
        }

        AppLog.Warn($"Prévia falhou ({FfmpegArgs.MaskCredentials(url)}): {LastSnapshotError}");
        return null;
    }
}

/// <summary>
/// Lê um stream com o ffmpeg, convertendo para BGRA de tamanho fixo e
/// entregando quadro a quadro. Reconecta sozinho, alternando TCP/UDP no modo
/// "auto", e reinicia se o stream ficar parado (watchdog).
/// </summary>
internal sealed class FfmpegStreamReader : IVideoStreamReader
{
    private readonly Func<string?> _ffmpegLocator;
    private readonly object _gate = new();
    private CancellationTokenSource? _cts;
    private Task? _loop;
    private Process? _process;

    public FfmpegStreamReader(Func<string?> ffmpegLocator)
    {
        _ffmpegLocator = ffmpegLocator;
    }

    public event Action<VideoFrame>? FrameReady;
    public event Action<VideoStreamState, string>? StateChanged;

    public VideoStreamState State { get; private set; } = VideoStreamState.Stopped;

    public void Start(string url, VideoStreamOptions? options = null)
    {
        Stop();
        var opts = options ?? new VideoStreamOptions();
        lock (_gate)
        {
            _cts = new CancellationTokenSource();
            var token = _cts.Token;
            _loop = Task.Run(() => RunAsync(url, opts, token), token);
        }
    }

    public void Stop()
    {
        CancellationTokenSource? cts;
        lock (_gate)
        {
            cts = _cts;
            _cts = null;
        }

        if (cts is null)
        {
            return;
        }

        cts.Cancel();
        FfmpegArgs.Kill(_process);
        cts.Dispose();
        SetState(VideoStreamState.Stopped, "Parado.");
    }

    public void Dispose() => Stop();

    private async Task RunAsync(string url, VideoStreamOptions opts, CancellationToken ct)
    {
        var transports = FfmpegArgs.TransportsFor(url, opts.RtspTransport).ToArray();
        var attempt = 0;
        var frameBytes = opts.Width * opts.Height * 4;
        var buffer = new byte[frameBytes];
        var frame = new VideoFrame(buffer, opts.Width, opts.Height);

        while (!ct.IsCancellationRequested)
        {
            var ffmpeg = _ffmpegLocator();
            if (ffmpeg is null)
            {
                SetState(VideoStreamState.Failed, "ffmpeg não encontrado. Instale-o para ver as câmeras.");
                return;
            }

            var transport = transports[attempt % transports.Length];
            SetState(attempt == 0 ? VideoStreamState.Connecting : VideoStreamState.Reconnecting,
                attempt == 0 ? "Conectando…" : $"Reconectando ({transport ?? "padrão"})…");

            var args = FfmpegArgs.Input(url, transport).Concat(new[]
            {
                "-an", "-sn", "-dn",
                "-vf",
                $"fps={opts.Fps},scale={opts.Width}:{opts.Height}:force_original_aspect_ratio=decrease," +
                $"pad={opts.Width}:{opts.Height}:(ow-iw)/2:(oh-ih)/2:black",
                "-pix_fmt", "bgra",
                "-f", "rawvideo",
                "pipe:1"
            });

            string lastError = string.Empty;
            var gotFrame = false;
            try
            {
                using var process = FfmpegArgs.Start(ffmpeg, args);
                _process = process;
                var errorTail = new StringBuilder();
                _ = Task.Run(async () =>
                {
                    try
                    {
                        string? line;
                        while ((line = await process.StandardError.ReadLineAsync()) is not null)
                        {
                            lock (errorTail)
                            {
                                errorTail.AppendLine(line);
                                if (errorTail.Length > 4000)
                                {
                                    errorTail.Remove(0, errorTail.Length - 4000);
                                }
                            }
                        }
                    }
                    catch
                    {
                        // processo encerrado
                    }
                }, CancellationToken.None);

                var stdout = process.StandardOutput.BaseStream;
                while (!ct.IsCancellationRequested)
                {
                    using var stall = CancellationTokenSource.CreateLinkedTokenSource(ct);
                    stall.CancelAfter(opts.StallTimeout);
                    try
                    {
                        await stdout.ReadExactlyAsync(buffer.AsMemory(0, frameBytes), stall.Token);
                    }
                    catch (OperationCanceledException) when (!ct.IsCancellationRequested)
                    {
                        lastError = gotFrame
                            ? "Stream parou de enviar imagens."
                            : "Tempo esgotado ao conectar no stream.";
                        break;
                    }
                    catch (EndOfStreamException)
                    {
                        break;
                    }

                    if (!gotFrame)
                    {
                        gotFrame = true;
                        attempt = 0;
                        SetState(VideoStreamState.Playing, "Ao vivo");
                    }

                    try
                    {
                        FrameReady?.Invoke(frame);
                    }
                    catch (Exception ex)
                    {
                        AppLog.Error("Erro ao exibir quadro", ex);
                    }
                }

                FfmpegArgs.Kill(process);
                if (string.IsNullOrEmpty(lastError))
                {
                    await Task.Delay(200, CancellationToken.None);
                    lock (errorTail)
                    {
                        lastError = FfmpegArgs.Explain(errorTail.ToString());
                    }
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                lastError = ex.Message;
            }
            finally
            {
                _process = null;
            }

            if (ct.IsCancellationRequested)
            {
                break;
            }

            attempt++;
            AppLog.Warn($"Stream {FfmpegArgs.MaskCredentials(url)} ({transport ?? "padrão"}): {lastError}");
            SetState(VideoStreamState.Reconnecting, $"{lastError} Tentando novamente…");

            try
            {
                await Task.Delay(opts.ReconnectDelay, ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private void SetState(VideoStreamState state, string message)
    {
        State = state;
        try
        {
            StateChanged?.Invoke(state, message);
        }
        catch
        {
            // handler da UI nunca derruba o leitor
        }
    }
}

/// <summary>Montagem segura de argumentos do ffmpeg e tradução de erros.</summary>
internal static partial class FfmpegArgs
{
    public static IEnumerable<string?> TransportsFor(string url, string mode)
    {
        if (!url.StartsWith("rtsp", StringComparison.OrdinalIgnoreCase))
        {
            return new string?[] { null };
        }

        return mode.ToLowerInvariant() switch
        {
            "tcp" => new string?[] { "tcp" },
            "udp" => new string?[] { "udp" },
            _ => new string?[] { "tcp", "udp" }
        };
    }

    public static IEnumerable<string> Input(string url, string? transport)
    {
        var args = new List<string> { "-hide_banner", "-loglevel", "error", "-nostdin" };
        if (transport is not null)
        {
            args.Add("-rtsp_transport");
            args.Add(transport);
        }

        args.AddRange(new[]
        {
            "-fflags", "nobuffer",
            "-flags", "low_delay",
            "-probesize", "1000000",
            "-analyzeduration", "2000000",
            "-i", url
        });
        return args;
    }

    /// <summary>Inicia o ffmpeg com <c>ArgumentList</c> (sem shell: sem injeção).</summary>
    public static Process Start(string ffmpeg, IEnumerable<string> args)
    {
        var psi = new ProcessStartInfo(ffmpeg)
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = false,
            CreateNoWindow = true
        };
        foreach (var a in args)
        {
            psi.ArgumentList.Add(a);
        }

        return Process.Start(psi) ?? throw new InvalidOperationException("Falha ao iniciar o ffmpeg.");
    }

    public static void Kill(Process? process)
    {
        try
        {
            if (process is { HasExited: false })
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch
        {
            // já encerrado
        }
    }

    /// <summary>Converte a saída de erro do ffmpeg numa mensagem curta e útil.</summary>
    public static string Explain(string stderr)
    {
        var s = stderr ?? string.Empty;
        if (s.Contains("401", StringComparison.Ordinal) || s.Contains("Unauthorized", StringComparison.OrdinalIgnoreCase))
        {
            return "Usuário ou senha da câmera inválidos (401).";
        }

        if (s.Contains("403", StringComparison.Ordinal) || s.Contains("Forbidden", StringComparison.OrdinalIgnoreCase))
        {
            return "O servidor recusou o acesso ao stream (403).";
        }

        if (s.Contains("404", StringComparison.Ordinal) || s.Contains("Not Found", StringComparison.OrdinalIgnoreCase))
        {
            return "Stream não encontrado no servidor (404) — confira o caminho da URL.";
        }

        if (s.Contains("Connection refused", StringComparison.OrdinalIgnoreCase))
        {
            return "Conexão recusada — a porta RTSP está fechada ou o IP está errado.";
        }

        if (s.Contains("timed out", StringComparison.OrdinalIgnoreCase) || s.Contains("Timeout", StringComparison.OrdinalIgnoreCase))
        {
            return "Tempo esgotado — câmera inacessível nesta rede.";
        }

        if (s.Contains("Name or service not known", StringComparison.OrdinalIgnoreCase) ||
            s.Contains("Failed to resolve", StringComparison.OrdinalIgnoreCase))
        {
            return "Não foi possível resolver o endereço da câmera (DNS).";
        }

        var last = s.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).LastOrDefault();
        return string.IsNullOrWhiteSpace(last) ? "O stream encerrou." : MaskCredentials(last);
    }

    /// <summary>Remove usuário/senha embutidos em URLs antes de registrar em log.</summary>
    public static string MaskCredentials(string text) => CredentialRegex().Replace(text, "://***:***@");

    [GeneratedRegex(@"://[^/@\s:]+(:[^/@\s]*)?@")]
    private static partial Regex CredentialRegex();
}
