namespace RedeScan.Core.Models;

public enum LoadTestProtocol
{
    Udp,
    Http,
    Tcp
}

public class DdosAttackConfig
{
    public string TargetHost { get; set; } = "127.0.0.1";
    public int TargetPort { get; set; } = 80;
    public LoadTestProtocol Protocol { get; set; } = LoadTestProtocol.Udp;
    public int Concurrency { get; set; } = 10;
    public int PacketSizeBytes { get; set; } = 1024;
    public int DurationSeconds { get; set; } = 30;
    public int DelayBetweenBurstsMs { get; set; } = 10;
    public string? HttpPayloadOrPath { get; set; } = "/";
}

public class DdosMetrics
{
    public long PacketsSent { get; set; }
    public long BytesSent { get; set; }
    public long RequestsSucceeded { get; set; }
    public long RequestsFailed { get; set; }
    public double PacketsPerSecond { get; set; }
    public double MegabitsPerSecond { get; set; }
    public TimeSpan ElapsedTime { get; set; }
    public bool IsRunning { get; set; }
    public string? StatusMessage { get; set; }
}
