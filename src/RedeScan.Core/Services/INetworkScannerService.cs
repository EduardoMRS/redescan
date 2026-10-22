using RedeScan.Core.Models;

namespace RedeScan.Core.Services;

public interface INetworkScannerService
{
    Task<IReadOnlyList<NetworkHost>> ScanSubnetAsync(
        string subnetPrefix, 
        int startHost = 1, 
        int endHost = 254, 
        int timeoutMs = 800, 
        int maxConcurrency = 40,
        bool resolveDns = true,
        bool probeWebPorts = false,
        IProgress<NetworkHost>? progress = null, 
        CancellationToken ct = default);
}
