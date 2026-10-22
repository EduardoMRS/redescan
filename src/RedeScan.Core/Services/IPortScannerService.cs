using RedeScan.Core.Models;

namespace RedeScan.Core.Services;

public interface IPortScannerService
{
    IReadOnlyDictionary<int, string> KnownPorts { get; }

    Task<PortScanResult> ScanSinglePortAsync(
        string ipAddress, 
        int port, 
        int timeoutMs = 500, 
        CancellationToken ct = default);

    Task<IReadOnlyList<PortScanResult>> ScanPortsAsync(
        string ipAddress, 
        IEnumerable<int> ports, 
        int timeoutMs = 500, 
        int maxConcurrency = 20, 
        IProgress<PortScanResult>? progress = null, 
        CancellationToken ct = default);
}
