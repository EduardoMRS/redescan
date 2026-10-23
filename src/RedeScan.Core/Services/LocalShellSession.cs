using System.Diagnostics;
using System.Text;
using RedeScan.Core.Diagnostics;

namespace RedeScan.Core.Services;

/// <summary>
/// Terminal do próprio dispositivo.
/// <list type="bullet">
/// <item>Linux: <c>bash</c> dentro de um pseudo-terminal criado pelo utilitário
/// <c>script</c> (util-linux) — prompt, eco, Ctrl+C e programas interativos
/// funcionam como num terminal comum. Sem <c>script</c>, usa <c>sh -i</c> com
/// pipes (a UI faz o eco).</item>
/// <item>Windows: <c>cmd.exe</c> em UTF-8.</item>
/// <item>Android/macOS: <c>sh -i</c> com pipes.</item>
/// </list>
/// O shell roda com o mesmo usuário do RedeScan (se aberto com sudo, é root).
/// </summary>
public sealed class LocalShellSession : ILocalShellSession
{
    private Process? _process;
    private StreamWriter? _stdin;
    private CancellationTokenSource? _cts;
    private int _readersAlive;
    private bool _usesPty;

    public event Action<string>? OutputReceived;
    public event Action<string?>? Disconnected;

    public bool IsConnected => _process is { HasExited: false };

    public bool NeedsLocalEcho => !_usesPty && !OperatingSystem.IsWindows();

    public string ShellDescription { get; private set; } = string.Empty;

    public Task StartAsync(CancellationToken ct = default)
    {
        var psi = BuildStartInfo();
        psi.UseShellExecute = false;
        psi.RedirectStandardInput = true;
        psi.RedirectStandardOutput = true;
        psi.RedirectStandardError = true;
        psi.CreateNoWindow = true;
        psi.StandardOutputEncoding = Encoding.UTF8;
        psi.StandardErrorEncoding = Encoding.UTF8;
        psi.WorkingDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        // Terminal simples: menos sequências de cor/posicionamento na saída.
        psi.Environment["TERM"] = "dumb";
        psi.Environment["PAGER"] = "cat";
        psi.Environment["GIT_PAGER"] = "cat";
        // Remove integrações de outros terminais/IDEs herdadas do processo pai
        // (ex.: PROMPT_COMMAND do VS Code), que quebram o prompt do shell novo.
        foreach (var key in psi.Environment.Keys.ToList())
        {
            if (key is "PROMPT_COMMAND" or "TERM_PROGRAM" or "TERM_PROGRAM_VERSION" ||
                key.StartsWith("VSCODE_", StringComparison.Ordinal) ||
                key.StartsWith("KIRO_", StringComparison.Ordinal))
            {
                psi.Environment.Remove(key);
            }
        }

        _process = Process.Start(psi) ?? throw new InvalidOperationException("Não foi possível iniciar o shell local.");
        _stdin = _process.StandardInput;
        _stdin.AutoFlush = true;
        _stdin.NewLine = OperatingSystem.IsWindows() ? "\r\n" : "\n";

        _cts = new CancellationTokenSource();
        _readersAlive = 2;
        _ = Task.Run(() => PumpAsync(_process.StandardOutput, _cts.Token));
        _ = Task.Run(() => PumpAsync(_process.StandardError, _cts.Token));
        AppLog.Info($"Terminal local iniciado: {ShellDescription}");
        return Task.CompletedTask;
    }

    private ProcessStartInfo BuildStartInfo()
    {
        if (OperatingSystem.IsWindows())
        {
            ShellDescription = "cmd.exe";
            var psi = new ProcessStartInfo("cmd.exe");
            // /K: mantém o cmd aberto; chcp 65001: saída em UTF-8.
            psi.ArgumentList.Add("/K");
            psi.ArgumentList.Add("chcp 65001>nul");
            return psi;
        }

        var shell = Environment.GetEnvironmentVariable("SHELL");
        if (string.IsNullOrWhiteSpace(shell) || !File.Exists(shell))
        {
            shell = File.Exists("/bin/bash") ? "/bin/bash" : "/system/bin/sh";
            if (!File.Exists(shell))
            {
                shell = "/bin/sh";
            }
        }

        if (OperatingSystem.IsLinux() && ExternalToolService.FindOnPath("script") is { } script)
        {
            _usesPty = true;
            ShellDescription = $"{shell} (PTY)";
            var psi = new ProcessStartInfo(script);
            foreach (var a in new[] { "-q", "-f", "-e", "-c", shell, "/dev/null" })
            {
                psi.ArgumentList.Add(a);
            }

            return psi;
        }

        ShellDescription = $"{shell} -i";
        var fallback = new ProcessStartInfo(shell);
        fallback.ArgumentList.Add("-i");
        return fallback;
    }

    private async Task PumpAsync(StreamReader reader, CancellationToken ct)
    {
        var buffer = new char[4096];
        try
        {
            int n;
            while (!ct.IsCancellationRequested && (n = await reader.ReadAsync(buffer.AsMemory(), ct)) > 0)
            {
                OutputReceived?.Invoke(new string(buffer, 0, n));
            }
        }
        catch (Exception) when (ct.IsCancellationRequested)
        {
            // encerramento normal
        }
        catch (Exception ex)
        {
            AppLog.Warn("Terminal local: " + ex.Message);
        }
        finally
        {
            if (Interlocked.Decrement(ref _readersAlive) == 0)
            {
                var code = _process is { HasExited: true } p ? p.ExitCode : (int?)null;
                Disconnected?.Invoke(code is null or 0 ? null : $"o shell terminou com código {code}");
            }
        }
    }

    public async Task SendAsync(string data, CancellationToken ct = default)
    {
        if (_stdin is null || !IsConnected)
        {
            return;
        }

        // Com PTY o Enter é \r (o terminal converte); com pipes o shell espera \n.
        var text = _usesPty
            ? data
            : data.Replace("\r\n", "\n").Replace('\r', '\n');
        if (OperatingSystem.IsWindows())
        {
            text = text.Replace("\n", "\r\n");
        }

        await _stdin.WriteAsync(text.AsMemory(), ct);
        await _stdin.FlushAsync(ct);
    }

    public void Disconnect()
    {
        _cts?.Cancel();
        try
        {
            if (_process is { HasExited: false })
            {
                _process.Kill(entireProcessTree: true);
            }
        }
        catch
        {
            // já encerrado
        }
    }

    public void Dispose()
    {
        Disconnect();
        _process?.Dispose();
        _process = null;
        _cts?.Dispose();
    }
}
