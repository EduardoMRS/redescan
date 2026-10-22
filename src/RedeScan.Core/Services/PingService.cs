using System.Net;
using System.Net.NetworkInformation;
using RedeScan.Core.Models;

namespace RedeScan.Core.Services;

public class PingService : IPingService
{
    private static readonly byte[] DefaultBuffer = new byte[32];

    public async Task<PingResult> PingAsync(
        string hostOrIp, 
        int timeoutMs = 1000, 
        byte[]? buffer = null, 
        CancellationToken ct = default)
    {
        buffer ??= DefaultBuffer;
        using var ping = new Ping();

        try
        {
            var reply = await ping.SendPingAsync(hostOrIp, timeoutMs, buffer);
            if (reply.Status == IPStatus.Success)
            {
                return new PingResult
                {
                    Success = true,
                    IpAddress = reply.Address?.ToString() ?? hostOrIp,
                    RoundtripTimeMs = reply.RoundtripTime,
                    Ttl = reply.Options?.Ttl ?? 0,
                    BufferSizeBytes = reply.Buffer?.Length ?? buffer.Length
                };
            }

            return new PingResult
            {
                Success = false,
                IpAddress = reply.Address?.ToString() ?? hostOrIp,
                ErrorMessage = reply.Status.ToString()
            };
        }
        catch (Exception ex)
        {
            return new PingResult
            {
                Success = false,
                IpAddress = hostOrIp,
                ErrorMessage = ex.Message
            };
        }
    }

    public async Task<IReadOnlyList<TracerouteHop>> TracerouteAsync(
        string hostOrIp, 
        int maxHops = 30, 
        int timeoutMs = 1000, 
        IProgress<TracerouteHop>? progress = null, 
        CancellationToken ct = default)
    {
        var hops = new List<TracerouteHop>();
        var buffer = DefaultBuffer;

        IPAddress targetIp;
        try
        {
            if (!IPAddress.TryParse(hostOrIp, out targetIp!))
            {
                var entry = await Dns.GetHostEntryAsync(hostOrIp, ct);
                targetIp = entry.AddressList.First(a => a.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork);
            }
        }
        catch
        {
            return hops;
        }

        for (int ttl = 1; ttl <= maxHops; ttl++)
        {
            if (ct.IsCancellationRequested)
                break;

            using var ping = new Ping();
            var options = new PingOptions(ttl, true);

            TracerouteHop hop;
            try
            {
                var reply = await ping.SendPingAsync(targetIp, timeoutMs, buffer, options);

                if (reply.Status == IPStatus.Success || reply.Status == IPStatus.TtlExpired)
                {
                    string? hostName = null;
                    if (reply.Address != null)
                    {
                        try
                        {
                            var dnsTask = Dns.GetHostEntryAsync(reply.Address);
                            if (await Task.WhenAny(dnsTask, Task.Delay(300, ct)) == dnsTask)
                            {
                                hostName = (await dnsTask).HostName;
                            }
                        }
                        catch
                        {
                            // Ignora falha de DNS reverso
                        }
                    }

                    hop = new TracerouteHop
                    {
                        HopNumber = ttl,
                        IpAddress = reply.Address?.ToString(),
                        Hostname = hostName,
                        RoundtripTimeMs = reply.RoundtripTime,
                        Success = true
                    };
                }
                else
                {
                    hop = new TracerouteHop
                    {
                        HopNumber = ttl,
                        Success = false
                    };
                }

                hops.Add(hop);
                progress?.Report(hop);

                if (reply.Status == IPStatus.Success)
                    break;
            }
            catch
            {
                hop = new TracerouteHop
                {
                    HopNumber = ttl,
                    Success = false
                };
                hops.Add(hop);
                progress?.Report(hop);
            }
        }

        return hops;
    }
}
