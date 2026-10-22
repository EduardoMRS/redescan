using System.Diagnostics;
using System.Runtime.InteropServices;
using RedeScan.Core.Models;

namespace RedeScan.Core.Services;

/// <summary>
/// Implementação multiplataforma de <see cref="INetworkConfigurationService"/>.
/// Linux usa <c>nmcli</c> (NetworkManager); Windows usa <c>netsh</c>.
/// Nunca aplica nada sem <c>apply = true</c>.
/// </summary>
public sealed class NetworkConfigurationService : INetworkConfigurationService
{
    private readonly bool _isLinux = RuntimeInformation.IsOSPlatform(OSPlatform.Linux);
    private readonly bool _isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

    public bool IsSupported => _isLinux || _isWindows;

    public string Backend => _isLinux ? "nmcli" : _isWindows ? "netsh" : "não suportado";

    public async Task<NetworkCommandResult> SetIpAsync(IpConfigRequest request, bool apply, CancellationToken ct = default)
    {
        if (!IsSupported)
        {
            return NotSupported();
        }

        string command;
        if (_isLinux)
        {
            if (request.Mode == IpAssignmentMode.Dynamic)
            {
                command = $"nmcli con mod \"{request.InterfaceName}\" ipv4.method auto " +
                          "ipv4.addresses \"\" ipv4.gateway \"\"";
            }
            else
            {
                var dns = request.DnsServers.Count > 0
                    ? $" ipv4.dns \"{string.Join(" ", request.DnsServers)}\""
                    : string.Empty;
                command = $"nmcli con mod \"{request.InterfaceName}\" ipv4.method manual " +
                          $"ipv4.addresses \"{request.AddressCidr}\" " +
                          $"ipv4.gateway \"{request.Gateway}\"{dns}";
            }

            // Reaplica a conexão para efetivar.
            command += $" && nmcli con up \"{request.InterfaceName}\"";
        }
        else // Windows
        {
            if (request.Mode == IpAssignmentMode.Dynamic)
            {
                command = $"netsh interface ip set address name=\"{request.InterfaceName}\" source=dhcp";
            }
            else
            {
                var (ip, mask) = SplitCidr(request.AddressCidr);
                command = $"netsh interface ip set address name=\"{request.InterfaceName}\" " +
                          $"static {ip} {mask} {request.Gateway}";
            }
        }

        return await RunOrPreviewAsync(command, apply, ct);
    }

    public async Task<NetworkCommandResult> SetDnsAsync(string interfaceName, IReadOnlyList<string> dnsServers, bool apply, CancellationToken ct = default)
    {
        if (!IsSupported)
        {
            return NotSupported();
        }

        string command;
        if (_isLinux)
        {
            var dns = string.Join(" ", dnsServers);
            command = $"nmcli con mod \"{interfaceName}\" ipv4.dns \"{dns}\" && " +
                      $"nmcli con up \"{interfaceName}\"";
        }
        else
        {
            // Primeiro DNS como primário; demais como secundários.
            var primary = dnsServers.Count > 0 ? dnsServers[0] : string.Empty;
            command = $"netsh interface ip set dns name=\"{interfaceName}\" static {primary}";
            for (var i = 1; i < dnsServers.Count; i++)
            {
                command += $" && netsh interface ip add dns name=\"{interfaceName}\" {dnsServers[i]} index={i + 1}";
            }
        }

        return await RunOrPreviewAsync(command, apply, ct);
    }

    public async Task<NetworkCommandResult> ConfigurePppoeAsync(PppoeRequest request, bool apply, CancellationToken ct = default)
    {
        if (!IsSupported)
        {
            return NotSupported();
        }

        string command;
        string previewCommand;
        if (_isLinux)
        {
            var real =
                $"nmcli con add type pppoe ifname \"{request.InterfaceName}\" " +
                $"con-name \"{request.ConnectionName}\" username \"{request.Username}\" " +
                $"password \"{request.Password}\" && nmcli con up \"{request.ConnectionName}\"";
            command = real;
            previewCommand = real.Replace($"password \"{request.Password}\"", "password \"********\"");
        }
        else
        {
            // Windows não cria PPPoE via netsh de forma direta; orienta o usuário.
            var real = $"rasdial \"{request.ConnectionName}\" \"{request.Username}\" \"{request.Password}\"";
            command = real;
            previewCommand = real.Replace($"\"{request.Password}\"", "\"********\"");
        }

        if (!apply)
        {
            return new NetworkCommandResult
            {
                Command = previewCommand,
                Applied = false,
                Success = false,
                RequiresPrivilege = true
            };
        }

        var result = await RunOrPreviewAsync(command, apply: true, ct);
        // Garante que a senha não vaze no campo Command devolvido.
        return result with { Command = previewCommand };
    }

    // ------------------------------------------------------------------ infra
    private static NetworkCommandResult NotSupported() => new()
    {
        Command = string.Empty,
        Applied = false,
        Success = false,
        Error = "Plataforma não suportada para configuração de rede.",
        RequiresPrivilege = false
    };

    private static (string Ip, string Mask) SplitCidr(string? cidr)
    {
        if (string.IsNullOrWhiteSpace(cidr) || !cidr.Contains('/'))
        {
            return (cidr ?? string.Empty, "255.255.255.0");
        }

        var parts = cidr.Split('/');
        var ip = parts[0];
        var prefix = int.TryParse(parts[1], out var p) ? p : 24;
        var maskBits = prefix == 0 ? 0u : 0xFFFFFFFFu << (32 - prefix);
        var mask = $"{(maskBits >> 24) & 0xFF}.{(maskBits >> 16) & 0xFF}.{(maskBits >> 8) & 0xFF}.{maskBits & 0xFF}";
        return (ip, mask);
    }

    private async Task<NetworkCommandResult> RunOrPreviewAsync(string command, bool apply, CancellationToken ct)
    {
        if (!apply)
        {
            return new NetworkCommandResult
            {
                Command = command,
                Applied = false,
                Success = false,
                RequiresPrivilege = true
            };
        }

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = _isWindows ? "cmd.exe" : "/bin/sh",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            if (_isWindows)
            {
                psi.ArgumentList.Add("/c");
                psi.ArgumentList.Add(command);
            }
            else
            {
                psi.ArgumentList.Add("-c");
                psi.ArgumentList.Add(command);
            }

            using var process = Process.Start(psi);
            if (process is null)
            {
                return new NetworkCommandResult { Command = command, Applied = true, Success = false, Error = "Falha ao iniciar o processo." };
            }

            var stdout = await process.StandardOutput.ReadToEndAsync(ct);
            var stderr = await process.StandardError.ReadToEndAsync(ct);
            await process.WaitForExitAsync(ct);

            return new NetworkCommandResult
            {
                Command = command,
                Applied = true,
                Success = process.ExitCode == 0,
                Output = string.IsNullOrWhiteSpace(stdout) ? null : stdout.Trim(),
                Error = string.IsNullOrWhiteSpace(stderr) ? null : stderr.Trim()
            };
        }
        catch (Exception ex)
        {
            return new NetworkCommandResult { Command = command, Applied = true, Success = false, Error = ex.Message };
        }
    }
}
