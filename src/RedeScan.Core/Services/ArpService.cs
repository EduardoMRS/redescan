using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace RedeScan.Core.Services;

/// <summary>
/// Implementação multiplataforma do <see cref="IArpService"/>. Lê a tabela ARP
/// do sistema via o utilitário nativo (<c>arp</c> no Windows, <c>ip neigh</c>/
/// <c>arp</c> no Linux) e extrai o MAC correspondente ao IP.
/// </summary>
public sealed partial class ArpService : IArpService
{
    [GeneratedRegex(@"([0-9a-fA-F]{2}([:-])){5}[0-9a-fA-F]{2}")]
    private static partial Regex MacRegex();

    public async Task<string?> GetMacAddressAsync(string ipAddress, CancellationToken ct = default)
    {
        try
        {
            var (fileName, arguments) = BuildCommand(ipAddress);
            var output = await RunAsync(fileName, arguments, ct);
            if (string.IsNullOrWhiteSpace(output))
            {
                return null;
            }

            // Procura a primeira linha que contém o IP e dela extrai o MAC.
            foreach (var line in output.Split('\n'))
            {
                if (!line.Contains(ipAddress, StringComparison.Ordinal))
                {
                    continue;
                }

                var match = MacRegex().Match(line);
                if (match.Success)
                {
                    return NormalizeMac(match.Value);
                }
            }

            // Fallback: alguns formatos imprimem só o MAC sem repetir o IP.
            var any = MacRegex().Match(output);
            return any.Success ? NormalizeMac(any.Value) : null;
        }
        catch
        {
            return null;
        }
    }

    private static (string FileName, string Arguments) BuildCommand(string ipAddress)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return ("arp", $"-a {ipAddress}");
        }

        // Linux/macOS: "ip neigh" é o mais moderno; "arp -n" cobre o resto.
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return ("ip", $"neigh show {ipAddress}");
        }

        return ("arp", $"-n {ipAddress}");
    }

    private static async Task<string> RunAsync(string fileName, string arguments, CancellationToken ct)
    {
        var psi = new ProcessStartInfo(fileName, arguments)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(psi);
        if (process is null)
        {
            return string.Empty;
        }

        var output = await process.StandardOutput.ReadToEndAsync(ct);
        await process.WaitForExitAsync(ct);
        return output;
    }

    private static string NormalizeMac(string raw) =>
        raw.Replace('-', ':').ToUpperInvariant();
}
