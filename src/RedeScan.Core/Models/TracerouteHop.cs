namespace RedeScan.Core.Models;

public record TracerouteHop
{
    public int HopNumber { get; init; }
    public string? IpAddress { get; init; }
    public string? Hostname { get; init; }
    public long RoundtripTimeMs { get; init; }
    public bool Success { get; init; }
}
