using System.Diagnostics;
using System.Runtime.InteropServices;

namespace RedeScan.Core.Services;

/// <summary>
/// Implementação multiplataforma de <see cref="IRemoteAccessService"/>.
/// SSH/Telnet abrem num terminal do sistema; Winbox é lançado diretamente
/// (nativo no Windows, via Wine no Linux quando aplicável).
/// </summary>
public sealed class RemoteAccessService : IRemoteAccessService
{
    private static bool IsWindows => RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
    private static bool IsLinux => RuntimeInformation.IsOSPlatform(OSPlatform.Linux);
    private static bool IsMac => RuntimeInformation.IsOSPlatform(OSPlatform.OSX);

    private readonly IExternalToolService _tools;

    public RemoteAccessService(IExternalToolService tools)
    {
        _tools = tools;
    }

    public bool IsWinboxAvailable => _tools.Find(ExternalTool.WinBox) is not null;

    public RemoteAccessResult OpenSsh(string host, int port = 22, string? username = null)
    {
        var target = string.IsNullOrWhiteSpace(username) ? host : $"{username}@{host}";
        var sshCmd = port == 22 ? $"ssh {target}" : $"ssh {target} -p {port}";
        return OpenInTerminal(sshCmd);
    }

    public RemoteAccessResult OpenTelnet(string host, int port = 23)
    {
        var telnetCmd = port == 23 ? $"telnet {host}" : $"telnet {host} {port}";
        return OpenInTerminal(telnetCmd);
    }

    public async Task<RemoteAccessResult> OpenWinboxAsync(
        string hostOrMac,
        string? username = null,
        IProgress<ToolDownloadProgress>? progress = null,
        CancellationToken ct = default)
    {
        string winbox;
        try
        {
            winbox = await _tools.EnsureAsync(ExternalTool.WinBox, progress, ct);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return new RemoteAccessResult
            {
                Launched = false,
                Command = $"WinBox {hostOrMac}",
                Error = $"Não foi possível obter o WinBox: {ex.Message}"
            };
        }

        try
        {
            // WinBox 4: "WinBox <connect-to> [usuário] [senha]". A senha NÃO é
            // passada pela linha de comando (ficaria visível na lista de
            // processos); o WinBox a pede na tela de login.
            var isLegacyExeOnLinux = IsLinux && winbox.EndsWith(".exe", StringComparison.OrdinalIgnoreCase);
            var psi = new ProcessStartInfo(isLegacyExeOnLinux ? "wine" : winbox)
            {
                UseShellExecute = false,
                // O WinBox 4 carrega a pasta "assets" relativa ao executável.
                WorkingDirectory = Path.GetDirectoryName(winbox) ?? Environment.CurrentDirectory
            };
            if (isLegacyExeOnLinux)
            {
                psi.ArgumentList.Add(winbox);
            }

            psi.ArgumentList.Add(hostOrMac);
            if (!string.IsNullOrWhiteSpace(username))
            {
                psi.ArgumentList.Add(username);
            }

            Process.Start(psi);
            return new RemoteAccessResult { Launched = true, Command = $"{winbox} {hostOrMac}" };
        }
        catch (Exception ex)
        {
            return new RemoteAccessResult { Launched = false, Command = $"{winbox} {hostOrMac}", Error = ex.Message };
        }
    }

    // ------------------------------------------------------------------ infra
    private static RemoteAccessResult OpenInTerminal(string command)
    {
        try
        {
            if (IsWindows)
            {
                // Abre num console novo que permanece aberto.
                Process.Start(new ProcessStartInfo("cmd.exe", $"/k {command}") { UseShellExecute = true });
                return new RemoteAccessResult { Launched = true, Command = command };
            }

            if (IsMac)
            {
                // Usa o AppleScript para abrir o Terminal com o comando.
                var osa = $"tell application \"Terminal\" to do script \"{command}\"";
                Process.Start(new ProcessStartInfo("osascript", $"-e '{osa}'") { UseShellExecute = false });
                return new RemoteAccessResult { Launched = true, Command = command };
            }

            // Linux: tenta os emuladores de terminal mais comuns, em ordem.
            foreach (var (term, args) in LinuxTerminals(command))
            {
                if (WhichExists(term))
                {
                    Process.Start(new ProcessStartInfo(term, args) { UseShellExecute = false });
                    return new RemoteAccessResult { Launched = true, Command = $"{term} {args}" };
                }
            }

            return new RemoteAccessResult
            {
                Launched = false,
                Command = command,
                Error = "Nenhum emulador de terminal encontrado (gnome-terminal, konsole, xterm…)."
            };
        }
        catch (Exception ex)
        {
            return new RemoteAccessResult { Launched = false, Command = command, Error = ex.Message };
        }
    }

    private static IEnumerable<(string Terminal, string Args)> LinuxTerminals(string command)
    {
        // "-- bash -lc '<cmd>; exec bash'" mantém o terminal aberto após o comando.
        var keepOpen = $"{command}; exec $SHELL";
        yield return ("gnome-terminal", $"-- bash -lc \"{keepOpen}\"");
        yield return ("konsole", $"-e bash -lc \"{keepOpen}\"");
        yield return ("xfce4-terminal", $"-e bash -lc \"{keepOpen}\"");
        yield return ("xterm", $"-e bash -lc \"{keepOpen}\"");
        yield return ("x-terminal-emulator", $"-e bash -lc \"{keepOpen}\"");
    }

    private static bool WhichExists(string exe)
    {
        try
        {
            var psi = new ProcessStartInfo("which", exe)
            {
                RedirectStandardOutput = true,
                UseShellExecute = false
            };
            using var p = Process.Start(psi);
            if (p is null) return false;
            var outp = p.StandardOutput.ReadToEnd();
            p.WaitForExit();
            return p.ExitCode == 0 && !string.IsNullOrWhiteSpace(outp);
        }
        catch
        {
            return false;
        }
    }
}
