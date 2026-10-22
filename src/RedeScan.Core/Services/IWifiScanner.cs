using RedeScan.Core.Models;

namespace RedeScan.Core.Services;

public interface IWifiScanner
{
    string PlatformName { get; }
    Task<bool> IsSupportedAsync(CancellationToken ct = default);
    Task<IReadOnlyList<WifiNetworkInfo>> GetAvailableNetworksAsync(CancellationToken ct = default);
}
