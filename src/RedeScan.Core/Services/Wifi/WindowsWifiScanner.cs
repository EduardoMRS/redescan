using System.Diagnostics;
using RedeScan.Core.Models;

namespace RedeScan.Core.Services.Wifi;

public class WindowsWifiScanner : IWifiScanner
{
    public string PlatformName => "Windows (netsh)";

    public Task<bool> IsSupportedAsync(CancellationToken ct = default)
    {
        // No Windows, netsh wlan existe por padrão
        return Task.FromResult(OperatingSystem.IsWindows());
    }

    public async Task<IReadOnlyList<WifiNetworkInfo>> GetAvailableNetworksAsync(CancellationToken ct = default)
    {
        if (!OperatingSystem.IsWindows())
            return Array.Empty<WifiNetworkInfo>();

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "netsh",
                Arguments = "wlan show networks mode=bssid",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi);
            if (process == null) return Array.Empty<WifiNetworkInfo>();

            var stdoutTask = process.StandardOutput.ReadToEndAsync(ct);
            await process.WaitForExitAsync(ct);
            var output = await stdoutTask;

            return WindowsNetshWifiParser.Parse(output);
        }
        catch
        {
            return Array.Empty<WifiNetworkInfo>();
        }
    }
}
