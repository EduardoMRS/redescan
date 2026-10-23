using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using RedeScan.Core.Models;

namespace RedeScan.Core.Services;

/// <summary>
/// Monitora as conexões e portas em escuta desta máquina (quem acessou qual
/// porta, de qual IP, e qual processo atende). Não captura pacotes — isso
/// exigiria root/libpcap —, mas lê a tabela de conexões do SO, o que funciona
/// sem privilégios (com root/administrador também mostra todos os processos).
/// </summary>
public interface IConnectionMonitorService
{
    /// <summary>Fonte usada para ler as conexões ("ss", "netstat" ou ".NET").</summary>
    string Backend { get; }

    /// <summary>Lê o instantâneo atual das conexões TCP/UDP.</summary>
    Task<IReadOnlyList<ConnectionEntry>> SnapshotAsync(CancellationToken ct = default);
}

/// <inheritdoc />
public sealed partial class ConnectionMonitorService : IConnectionMonitorService
{
    private static bool IsWindows => RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
    private static bool IsLinux => RuntimeInformation.IsOSPlatform(OSPlatform.Linux);

    private readonly Dictionary<int, string> _processNames = new();
    private string _backend = IsLinux ? "ss" : IsWindows ? "netstat" : ".NET";

    public string Backend => _backend;

    public async Task<IReadOnlyList<ConnectionEntry>> SnapshotAsync(CancellationToken ct = default)
    {
        try
        {
            if (IsLinux)
            {
                var output = await RunAsync("ss", new[] { "-H", "-tunap" }, ct);
                if (output is not null)
                {
                    _backend = "ss";
                    return Classify(ParseSs(output));
                }
            }
            else if (IsWindows)
            {
                var output = await RunAsync("netstat", new[] { "-ano" }, ct);
                if (output is not null)
                {
                    _backend = "netstat";
                    var parsed = ParseNetstat(output)
                        .Select(e => e with { ProcessName = e.ProcessId is { } pid ? ResolveProcessName(pid) : null })
                        .ToList();
                    return Classify(parsed);
                }
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            // cai no fallback gerenciado
        }

        _backend = ".NET";
        return Classify(ReadManaged());
    }

    // ------------------------------------------------------------- parsers
    /// <summary>
    /// Interpreta a saída de <c>ss -H -tunap</c>. Ex.:
    /// <c>tcp ESTAB 0 0 192.168.0.10:22 192.168.0.5:51234 users:(("sshd",pid=123,fd=4))</c>
    /// </summary>
    public static IReadOnlyList<ConnectionEntry> ParseSs(string output)
    {
        var list = new List<ConnectionEntry>();
        foreach (var raw in output.Split('\n'))
        {
            var line = raw.Trim();
            if (line.Length == 0)
            {
                continue;
            }

            var parts = WhitespaceRegex().Split(line, 7);
            if (parts.Length < 6)
            {
                continue;
            }

            var proto = parts[0].ToUpperInvariant();
            if (proto is not ("TCP" or "UDP"))
            {
                continue;
            }

            if (!TrySplitEndpoint(parts[4], out var localIp, out var localPort) ||
                !TrySplitEndpoint(parts[5], out var remoteIp, out var remotePort))
            {
                continue;
            }

            int? pid = null;
            string? name = null;
            if (parts.Length > 6)
            {
                var m = SsProcessRegex().Match(parts[6]);
                if (m.Success)
                {
                    name = m.Groups["name"].Value;
                    pid = int.Parse(m.Groups["pid"].Value, CultureInfo.InvariantCulture);
                }
            }

            list.Add(new ConnectionEntry
            {
                Protocol = proto,
                LocalAddress = localIp,
                LocalPort = localPort,
                RemoteAddress = remoteIp,
                RemotePort = remotePort,
                State = NormalizeState(parts[1]),
                ProcessId = pid,
                ProcessName = name
            });
        }

        return list;
    }

    /// <summary>
    /// Interpreta a saída de <c>netstat -ano</c> do Windows. Ex.:
    /// <c>TCP 192.168.0.10:139 0.0.0.0:0 LISTENING 4</c> /
    /// <c>UDP 0.0.0.0:123 *:* 1234</c>
    /// </summary>
    public static IReadOnlyList<ConnectionEntry> ParseNetstat(string output)
    {
        var list = new List<ConnectionEntry>();
        foreach (var raw in output.Split('\n'))
        {
            var parts = WhitespaceRegex().Split(raw.Trim());
            if (parts.Length < 4)
            {
                continue;
            }

            var proto = parts[0].ToUpperInvariant();
            if (proto is not ("TCP" or "UDP"))
            {
                continue;
            }

            if (!TrySplitEndpoint(parts[1], out var localIp, out var localPort) ||
                !TrySplitEndpoint(parts[2], out var remoteIp, out var remotePort))
            {
                continue;
            }

            string state;
            string pidText;
            if (proto == "TCP" && parts.Length >= 5)
            {
                state = NormalizeState(parts[3]);
                pidText = parts[4];
            }
            else
            {
                state = "UNCONN";
                pidText = parts[^1];
            }

            list.Add(new ConnectionEntry
            {
                Protocol = proto,
                LocalAddress = localIp,
                LocalPort = localPort,
                RemoteAddress = remoteIp,
                RemotePort = remotePort,
                State = state,
                ProcessId = int.TryParse(pidText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var pid) ? pid : null
            });
        }

        return list;
    }

    /// <summary>
    /// Define o sentido de cada conexão: portas em escuta; entradas (a porta
    /// local é uma porta em escuta desta máquina); demais são saídas.
    /// </summary>
    public static IReadOnlyList<ConnectionEntry> Classify(IReadOnlyList<ConnectionEntry> entries)
    {
        var listeningPorts = entries
            .Where(e => e.Protocol == "TCP" && e.State == "LISTEN")
            .Select(e => e.LocalPort)
            .ToHashSet();

        return entries.Select(e =>
        {
            ConnectionDirection dir;
            if (e.State == "LISTEN" ||
                (e.Protocol == "UDP" && e.RemotePort == 0 && ConnectionEntry.IsAnyAddress(e.RemoteAddress)))
            {
                dir = ConnectionDirection.Listening;
            }
            else if (e.Protocol == "TCP" && listeningPorts.Contains(e.LocalPort))
            {
                dir = ConnectionDirection.Inbound;
            }
            else if (e.Protocol == "TCP")
            {
                dir = ConnectionDirection.Outbound;
            }
            else
            {
                dir = ConnectionDirection.Unknown;
            }

            return e with { Direction = dir };
        }).ToList();
    }

    /// <summary>Separa "ip:porta" (IPv4, "[ipv6]:porta", "*:porta", "ipv6%iface:porta").</summary>
    public static bool TrySplitEndpoint(string text, out string address, out int port)
    {
        address = string.Empty;
        port = 0;
        var idx = text.LastIndexOf(':');
        if (idx <= 0 || idx == text.Length - 1)
        {
            return false;
        }

        var portText = text[(idx + 1)..];
        address = text[..idx].Trim('[', ']');
        var pct = address.IndexOf('%');
        if (pct >= 0)
        {
            address = address[..pct];
        }

        if (portText == "*")
        {
            port = 0;
            return true;
        }

        return int.TryParse(portText, NumberStyles.Integer, CultureInfo.InvariantCulture, out port);
    }

    private static string NormalizeState(string state) => state.ToUpperInvariant() switch
    {
        "LISTENING" => "LISTEN",
        "ESTABLISHED" => "ESTAB",
        "TIME_WAIT" or "TIME-WAIT" => "TIME-WAIT",
        "CLOSE_WAIT" or "CLOSE-WAIT" => "CLOSE-WAIT",
        "SYN_SENT" or "SYN-SENT" => "SYN-SENT",
        "SYN_RECEIVED" or "SYN-RECV" => "SYN-RECV",
        "FIN_WAIT_1" or "FIN-WAIT-1" => "FIN-WAIT-1",
        "FIN_WAIT_2" or "FIN-WAIT-2" => "FIN-WAIT-2",
        var s => s
    };

    // ------------------------------------------------------------ fallback
    private static IReadOnlyList<ConnectionEntry> ReadManaged()
    {
        var props = IPGlobalProperties.GetIPGlobalProperties();
        var list = new List<ConnectionEntry>();

        foreach (var l in props.GetActiveTcpListeners())
        {
            list.Add(new ConnectionEntry
            {
                Protocol = "TCP", LocalAddress = l.Address.ToString(), LocalPort = l.Port,
                RemoteAddress = "0.0.0.0", State = "LISTEN"
            });
        }

        foreach (var c in props.GetActiveTcpConnections())
        {
            list.Add(new ConnectionEntry
            {
                Protocol = "TCP",
                LocalAddress = c.LocalEndPoint.Address.ToString(), LocalPort = c.LocalEndPoint.Port,
                RemoteAddress = c.RemoteEndPoint.Address.ToString(), RemotePort = c.RemoteEndPoint.Port,
                State = NormalizeState(c.State.ToString())
            });
        }

        foreach (var u in props.GetActiveUdpListeners())
        {
            list.Add(new ConnectionEntry
            {
                Protocol = "UDP", LocalAddress = u.Address.ToString(), LocalPort = u.Port,
                RemoteAddress = IPAddress.Any.ToString(), State = "UNCONN"
            });
        }

        return list;
    }

    private string? ResolveProcessName(int pid)
    {
        if (pid <= 0)
        {
            return pid == 0 ? "Sistema (ocioso)" : null;
        }

        if (_processNames.TryGetValue(pid, out var cached))
        {
            return cached;
        }

        string? name;
        try
        {
            using var p = System.Diagnostics.Process.GetProcessById(pid);
            name = p.ProcessName;
        }
        catch
        {
            name = null;
        }

        if (_processNames.Count > 2000)
        {
            _processNames.Clear();
        }

        _processNames[pid] = name ?? $"PID {pid}";
        return name;
    }

    private static async Task<string?> RunAsync(string file, string[] args, CancellationToken ct)
    {
        try
        {
            var psi = new ProcessStartInfo(file)
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            foreach (var a in args)
            {
                psi.ArgumentList.Add(a);
            }

            using var p = System.Diagnostics.Process.Start(psi);
            if (p is null)
            {
                return null;
            }

            var output = await p.StandardOutput.ReadToEndAsync(ct);
            await p.WaitForExitAsync(ct);
            return p.ExitCode == 0 ? output : null;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            return null;
        }
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();

    [GeneratedRegex("\\(\"(?<name>[^\"]+)\",pid=(?<pid>\\d+)")]
    private static partial Regex SsProcessRegex();
}
