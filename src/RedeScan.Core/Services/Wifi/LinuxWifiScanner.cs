using System.Diagnostics;
using RedeScan.Core.Models;

namespace RedeScan.Core.Services.Wifi;

public class LinuxWifiScanner : IWifiScanner
{
    public string PlatformName => "Linux (nmcli)";

    public async Task<bool> IsSupportedAsync(CancellationToken ct = default)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "nmcli",
                Arguments = "-v",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi);
            if (process == null) return false;

            await process.WaitForExitAsync(ct);
            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    public async Task<IReadOnlyList<WifiNetworkInfo>> GetAvailableNetworksAsync(CancellationToken ct = default)
    {
        try
        {
            // Tenta listar com rescan primeiro
            var output = await RunNmcliAsync("-t -f IN-USE,SSID,BSSID,CHAN,FREQ,SIGNAL,SECURITY dev wifi list --rescan yes", ct);
            
            // Se falhou ou vazio, tenta sem rescan
            if (string.IsNullOrWhiteSpace(output))
            {
                output = await RunNmcliAsync("-t -f IN-USE,SSID,BSSID,CHAN,FREQ,SIGNAL,SECURITY dev wifi list", ct);
            }

            return LinuxNmcliWifiParser.Parse(output);
        }
        catch
        {
            return Array.Empty<WifiNetworkInfo>();
        }
    }

    private static async Task<string> RunNmcliAsync(string arguments, CancellationToken ct)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "nmcli",
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(psi);
        if (process == null) return string.Empty;

        var stdoutTask = process.StandardOutput.ReadToEndAsync(ct);
        await process.WaitForExitAsync(ct);
        return await stdoutTask;
    }
}
