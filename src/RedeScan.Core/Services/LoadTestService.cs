using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using RedeScan.Core.Models;

namespace RedeScan.Core.Services;

public class LoadTestService : ILoadTestService
{
    private readonly HttpClient _httpClient;
    private long _packetsSent;
    private long _bytesSent;
    private long _requestsSucceeded;
    private long _requestsFailed;
    private bool _isRunning;

    public LoadTestService(HttpClient? httpClient = null)
    {
        _httpClient = httpClient ?? new HttpClient(new SocketsHttpHandler
        {
            PooledConnectionLifetime = TimeSpan.FromMinutes(2),
            ConnectTimeout = TimeSpan.FromSeconds(2)
        });
    }

    public bool IsRunning => _isRunning;

    public async Task<DdosMetrics> RunLoadTestAsync(
        DdosAttackConfig config, 
        IProgress<DdosMetrics>? progress = null, 
        CancellationToken ct = default)
    {
        if (_isRunning)
            throw new InvalidOperationException("Um teste de carga já está em execução.");

        _isRunning = true;
        Interlocked.Exchange(ref _packetsSent, 0);
        Interlocked.Exchange(ref _bytesSent, 0);
        Interlocked.Exchange(ref _requestsSucceeded, 0);
        Interlocked.Exchange(ref _requestsFailed, 0);

        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        if (config.DurationSeconds > 0)
        {
            linkedCts.CancelAfter(TimeSpan.FromSeconds(config.DurationSeconds));
        }

        var token = linkedCts.Token;
        var stopwatch = Stopwatch.StartNew();
        long lastPackets = 0;
        long lastBytes = 0;
        var lastSampleTime = stopwatch.Elapsed;

        // Monitor periódico de métricas
        var metricsTimerTask = Task.Run(async () =>
        {
            while (!token.IsCancellationRequested && _isRunning)
            {
                await Task.Delay(500, CancellationToken.None);

                var currentElapsed = stopwatch.Elapsed;
                var currentPackets = Interlocked.Read(ref _packetsSent);
                var currentBytes = Interlocked.Read(ref _bytesSent);

                var timeDeltaSeconds = (currentElapsed - lastSampleTime).TotalSeconds;
                if (timeDeltaSeconds > 0)
                {
                    var pps = (currentPackets - lastPackets) / timeDeltaSeconds;
                    var mbps = ((currentBytes - lastBytes) * 8.0) / (timeDeltaSeconds * 1_000_000.0);

                    lastPackets = currentPackets;
                    lastBytes = currentBytes;
                    lastSampleTime = currentElapsed;

                    var currentMetrics = new DdosMetrics
                    {
                        PacketsSent = currentPackets,
                        BytesSent = currentBytes,
                        RequestsSucceeded = Interlocked.Read(ref _requestsSucceeded),
                        RequestsFailed = Interlocked.Read(ref _requestsFailed),
                        PacketsPerSecond = pps,
                        MegabitsPerSecond = mbps,
                        ElapsedTime = currentElapsed,
                        IsRunning = true,
                        StatusMessage = "Executando teste de carga..."
                    };

                    progress?.Report(currentMetrics);
                }
            }
        }, CancellationToken.None);

        try
        {
            var workers = new List<Task>();
            for (int i = 0; i < Math.Max(1, config.Concurrency); i++)
            {
                workers.Add(Task.Run(async () =>
                {
                    if (config.Protocol == LoadTestProtocol.Udp)
                    {
                        await RunUdpWorkerAsync(config, token);
                    }
                    else if (config.Protocol == LoadTestProtocol.Http)
                    {
                        await RunHttpWorkerAsync(config, token);
                    }
                    else
                    {
                        await RunTcpWorkerAsync(config, token);
                    }
                }, token));
            }

            await Task.WhenAll(workers);
        }
        catch (OperationCanceledException)
        {
            // Parada normal por tempo ou cancelamento
        }
        finally
        {
            _isRunning = false;
            stopwatch.Stop();
            try { await metricsTimerTask; } catch { }
        }

        var totalSeconds = Math.Max(0.001, stopwatch.Elapsed.TotalSeconds);
        var finalPackets = Interlocked.Read(ref _packetsSent);
        var finalBytes = Interlocked.Read(ref _bytesSent);

        var finalMetrics = new DdosMetrics
        {
            PacketsSent = finalPackets,
            BytesSent = finalBytes,
            RequestsSucceeded = Interlocked.Read(ref _requestsSucceeded),
            RequestsFailed = Interlocked.Read(ref _requestsFailed),
            PacketsPerSecond = finalPackets / totalSeconds,
            MegabitsPerSecond = (finalBytes * 8.0) / (totalSeconds * 1_000_000.0),
            ElapsedTime = stopwatch.Elapsed,
            IsRunning = false,
            StatusMessage = "Teste concluído."
        };

        progress?.Report(finalMetrics);
        return finalMetrics;
    }

    private async Task RunUdpWorkerAsync(DdosAttackConfig config, CancellationToken token)
    {
        using var udpClient = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        IPAddress targetIp;
        if (!IPAddress.TryParse(config.TargetHost, out targetIp!))
        {
            var entry = await Dns.GetHostEntryAsync(config.TargetHost, token);
            targetIp = entry.AddressList.First(a => a.AddressFamily == AddressFamily.InterNetwork);
        }

        var endPoint = new IPEndPoint(targetIp, config.TargetPort);
        var buffer = new byte[Math.Max(16, config.PacketSizeBytes)];
        Random.Shared.NextBytes(buffer);

        while (!token.IsCancellationRequested)
        {
            try
            {
                await udpClient.SendToAsync(buffer, SocketFlags.None, endPoint, token);
                Interlocked.Increment(ref _packetsSent);
                Interlocked.Add(ref _bytesSent, buffer.Length);
                Interlocked.Increment(ref _requestsSucceeded);
            }
            catch
            {
                Interlocked.Increment(ref _requestsFailed);
            }

            if (config.DelayBetweenBurstsMs > 0)
            {
                await Task.Delay(config.DelayBetweenBurstsMs, token);
            }
        }
    }

    private async Task RunHttpWorkerAsync(DdosAttackConfig config, CancellationToken token)
    {
        var url = config.TargetHost.StartsWith("http", StringComparison.OrdinalIgnoreCase)
            ? config.TargetHost
            : $"http://{config.TargetHost}:{config.TargetPort}{config.HttpPayloadOrPath}";

        while (!token.IsCancellationRequested)
        {
            try
            {
                using var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, token);
                Interlocked.Increment(ref _packetsSent);
                Interlocked.Increment(ref _requestsSucceeded);
            }
            catch
            {
                Interlocked.Increment(ref _requestsFailed);
            }

            if (config.DelayBetweenBurstsMs > 0)
            {
                await Task.Delay(config.DelayBetweenBurstsMs, token);
            }
        }
    }

    private async Task RunTcpWorkerAsync(DdosAttackConfig config, CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                using var client = new TcpClient();
                using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(token);
                timeoutCts.CancelAfter(800);

                await client.ConnectAsync(config.TargetHost, config.TargetPort, timeoutCts.Token);
                Interlocked.Increment(ref _packetsSent);
                Interlocked.Increment(ref _requestsSucceeded);
            }
            catch
            {
                Interlocked.Increment(ref _requestsFailed);
            }

            if (config.DelayBetweenBurstsMs > 0)
            {
                await Task.Delay(config.DelayBetweenBurstsMs, token);
            }
        }
    }
}
