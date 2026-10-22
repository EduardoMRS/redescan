namespace RedeScan.Core.Models;

public record PingResult
{
    public bool Success { get; init; }
    public string IpAddress { get; init; } = string.Empty;
    public long RoundtripTimeMs { get; init; }
    public int Ttl { get; init; }
    public int BufferSizeBytes { get; init; }
    public string? ErrorMessage { get; init; }
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}
