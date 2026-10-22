namespace RedeScan.Core.Models;

public record PortScanResult
{
    public string IpAddress { get; init; } = string.Empty;
    public int Port { get; init; }
    public bool IsOpen { get; init; }
    public string ServiceName { get; init; } = string.Empty;
    public long ResponseTimeMs { get; init; }
}
