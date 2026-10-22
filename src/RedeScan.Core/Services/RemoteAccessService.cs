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

    private readonly string? _winboxPath;

    public RemoteAccessService()
    {
        _winboxPath = LocateWinbox();
    }

    public bool IsWinboxAvailable => _winboxPath is not null;

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

    public RemoteAccessResult OpenWinbox(string hostOrMac)
    {
        if (_winboxPath is null)
        {
            return new RemoteAccessResult
            {
                Launched = false,
                Command = $"winbox {hostOrMac}",
                Error = "Winbox não encontrado no sistema (instale ou adicione ao PATH)."
            };
        }

        try
        {
            // No Linux, um winbox .exe precisa do Wine; um binário nativo roda direto.
            if (IsLinux && _winboxPath.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            {
                Process.Start(new ProcessStartInfo("wine", $"\"{_winboxPath}\" {hostOrMac}") { UseShellExecute = false });
                return new RemoteAccessResult { Launched = true, Command = $"wine {_winboxPath} {hostOrMac}" };
            }

            Process.Start(new ProcessStartInfo(_winboxPath, hostOrMac) { UseShellExecute = true });
            return new RemoteAccessResult { Launched = true, Command = $"{_winboxPath} {hostOrMac}" };
        }
        catch (Exception ex)
        {
            return new RemoteAccessResult { Launched = false, Command = $"{_winboxPath} {hostOrMac}", Error = ex.Message };
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

    private static string? LocateWinbox()
    {
        // 1) No PATH (nome nativo ou .exe).
        foreach (var name in new[] { "winbox", "winbox64", "winbox.exe", "winbox64.exe" })
        {
            var inPath = FindOnPath(name);
            if (inPath is not null)
            {
                return inPath;
            }
        }

        // 2) Locais comuns de instalação.
        var candidates = new List<string>();
        if (IsWindows)
        {
            var pf = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            var pfx86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
            candidates.Add(Path.Combine(pf, "winbox.exe"));
            candidates.Add(Path.Combine(pfx86, "winbox.exe"));
        }
        else
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            candidates.Add(Path.Combine(home, ".local", "bin", "winbox"));
            candidates.Add(Path.Combine(home, "winbox", "winbox.exe"));
            candidates.Add("/opt/winbox/winbox.exe");
            candidates.Add("/usr/local/bin/winbox");
        }

        return candidates.FirstOrDefault(File.Exists);
    }

    private static string? FindOnPath(string exe)
    {
        var pathVar = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrEmpty(pathVar))
        {
            return null;
        }

        var sep = IsWindows ? ';' : ':';
        foreach (var dir in pathVar.Split(sep, StringSplitOptions.RemoveEmptyEntries))
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
                // ignora entradas inválidas no PATH
            }
        }
        return null;
    }
}
