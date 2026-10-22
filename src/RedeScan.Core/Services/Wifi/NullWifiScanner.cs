using RedeScan.Core.Models;

namespace RedeScan.Core.Services.Wifi;

public class NullWifiScanner : IWifiScanner
{
    public string PlatformName => "Não suportado / Sem adaptador Wi-Fi";

    public Task<bool> IsSupportedAsync(CancellationToken ct = default) => Task.FromResult(false);

    public Task<IReadOnlyList<WifiNetworkInfo>> GetAvailableNetworksAsync(CancellationToken ct = default)
    {
        return Task.FromResult<IReadOnlyList<WifiNetworkInfo>>(Array.Empty<WifiNetworkInfo>());
    }
}
