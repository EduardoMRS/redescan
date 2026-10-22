using RedeScan.Core.Models;
using RedeScan.Core.Services;

namespace RedeScan.Core.Tests;

public class LoadTestServiceTests
{
    [Fact]
    public async Task RunLoadTest_ExecutesUdpFloodAndCollectsMetrics()
    {
        var service = new LoadTestService();
        var config = new DdosAttackConfig
        {
            TargetHost = "127.0.0.1",
            TargetPort = 55555,
            Protocol = LoadTestProtocol.Udp,
            Concurrency = 2,
            DurationSeconds = 1,
            PacketSizeBytes = 128,
            DelayBetweenBurstsMs = 1
        };

        var reportedMetrics = new List<DdosMetrics>();
        var progress = new Progress<DdosMetrics>(m => reportedMetrics.Add(m));

        var finalMetrics = await service.RunLoadTestAsync(config, progress);

        Assert.NotNull(finalMetrics);
        Assert.False(finalMetrics.IsRunning);
        Assert.True(finalMetrics.PacketsSent > 0, "Deveria ter enviado pelo menos 1 pacote UDP.");
        Assert.True(finalMetrics.BytesSent > 0, "Deveria ter bytes registrados.");
    }

    [Fact]
    public async Task RunLoadTest_CanBeCancelledPrematurely()
    {
        var service = new LoadTestService();
        var config = new DdosAttackConfig
        {
            TargetHost = "127.0.0.1",
            TargetPort = 55555,
            Protocol = LoadTestProtocol.Udp,
            Concurrency = 2,
            DurationSeconds = 10,
            DelayBetweenBurstsMs = 5
        };

        using var cts = new CancellationTokenSource();
        cts.CancelAfter(400); // Cancela em 400ms

        var sw = System.Diagnostics.Stopwatch.StartNew();
        var metrics = await service.RunLoadTestAsync(config, ct: cts.Token);
        sw.Stop();

        Assert.False(service.IsRunning);
        Assert.True(sw.ElapsedMilliseconds < 2500, "O teste deveria ter sido cancelado em menos de 2.5s");
    }
}
