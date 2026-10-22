using RedeScan.Core.Models;

namespace RedeScan.Core.Services.Wifi;

public class PlatformWifiScanner : IWifiScanner
{
    private readonly IWifiScanner _underlyingScanner;

    public PlatformWifiScanner()
    {
        if (OperatingSystem.IsLinux())
        {
            _underlyingScanner = new LinuxWifiScanner();
        }
        else if (OperatingSystem.IsWindows())
        {
            _underlyingScanner = new WindowsWifiScanner();
        }
        else
        {
            _underlyingScanner = new NullWifiScanner();
        }
    }

    public string PlatformName => _underlyingScanner.PlatformName;

    public Task<bool> IsSupportedAsync(CancellationToken ct = default)
    {
        return _underlyingScanner.IsSupportedAsync(ct);
    }

    public Task<IReadOnlyList<WifiNetworkInfo>> GetAvailableNetworksAsync(CancellationToken ct = default)
    {
        return _underlyingScanner.GetAvailableNetworksAsync(ct);
    }
}
