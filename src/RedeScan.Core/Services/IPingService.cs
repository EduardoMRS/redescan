using RedeScan.Core.Models;

namespace RedeScan.Core.Services;

public interface IPingService
{
    Task<PingResult> PingAsync(string hostOrIp, int timeoutMs = 1000, byte[]? buffer = null, CancellationToken ct = default);
    Task<IReadOnlyList<TracerouteHop>> TracerouteAsync(
        string hostOrIp, 
        int maxHops = 30, 
        int timeoutMs = 1000, 
        IProgress<TracerouteHop>? progress = null, 
        CancellationToken ct = default);
}
