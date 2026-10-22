using RedeScan.Core.Models;

namespace RedeScan.Core.Services;

public interface ILoadTestService
{
    bool IsRunning { get; }
    Task<DdosMetrics> RunLoadTestAsync(
        DdosAttackConfig config, 
        IProgress<DdosMetrics>? progress = null, 
        CancellationToken ct = default);
}
