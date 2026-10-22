namespace RedeScan.Core.Models;

public record NetworkHost
{
    public string IpAddress { get; init; } = string.Empty;
    public string? Hostname { get; init; }
    public long RoundtripTimeMs { get; init; }
    public bool IsOnline { get; init; }
    public string? MacAddress { get; init; }
    public string? Vendor { get; init; }
    public DateTime DiscoveredAt { get; init; } = DateTime.UtcNow;
}
