using RedeScan.Core.Models;
using RedeScan.Core.Services;

namespace RedeScan.Core.Tests;

public class ConnectionMonitorTests
{
    private const string SsSample =
        "tcp   LISTEN 0      128          0.0.0.0:22         0.0.0.0:*     users:((\"sshd\",pid=812,fd=3))\n" +
        "tcp   ESTAB  0      0      192.168.0.10:22    192.168.0.55:51234 users:((\"sshd\",pid=9001,fd=4))\n" +
        "tcp   ESTAB  0      0      192.168.0.10:40100  142.250.78.4:443\n" +
        "tcp   LISTEN 0      4096            [::]:8080           [::]:*\n" +
        "udp   UNCONN 0      0            0.0.0.0:68         0.0.0.0:*\n" +
        "tcp   ESTAB  0      0      [::ffff:192.168.0.10]:8080 [::ffff:192.168.0.77]:60000\n";

    [Fact]
    public void ParseSs_ReadsEndpointsStateAndProcess()
    {
        var list = ConnectionMonitorService.ParseSs(SsSample);

        Assert.Equal(6, list.Count);
        var listen = list[0];
        Assert.Equal("TCP", listen.Protocol);
        Assert.Equal("LISTEN", listen.State);
        Assert.Equal(22, listen.LocalPort);
        Assert.Equal("sshd", listen.ProcessName);
        Assert.Equal(812, listen.ProcessId);

        var inbound = list[1];
        Assert.Equal("192.168.0.55", inbound.RemoteAddress);
        Assert.Equal(51234, inbound.RemotePort);
        Assert.Null(list[2].ProcessName); // sem privilégio, ss não mostra o processo

        Assert.Equal("::ffff:192.168.0.77", list[5].RemoteAddress);
        Assert.Equal(8080, list[5].LocalPort);
    }

    [Theory]
    [InlineData("127.0.0.1", true)]
    [InlineData("::ffff:127.0.0.1", true)]
    [InlineData("::1", true)]
    [InlineData("192.168.0.5", false)]
    [InlineData("::ffff:192.168.0.5", false)]
    public void IsLoopback_IncludesIpv4MappedAddresses(string remote, bool expected)
    {
        var entry = new ConnectionEntry { RemoteAddress = remote, RemotePort = 5000, Direction = ConnectionDirection.Inbound };
        Assert.Equal(expected, entry.IsLoopback);
    }

    [Fact]
    public void Classify_MarksInboundOutboundAndListening()
    {
        var list = ConnectionMonitorService.Classify(ConnectionMonitorService.ParseSs(SsSample));

        Assert.Equal(ConnectionDirection.Listening, list[0].Direction);
        Assert.Equal(ConnectionDirection.Inbound, list[1].Direction);   // alguém acessou o SSH
        Assert.Equal(ConnectionDirection.Outbound, list[2].Direction);  // esta máquina → 443
        Assert.Equal(ConnectionDirection.Listening, list[4].Direction); // UDP sem par
        Assert.Equal(ConnectionDirection.Inbound, list[5].Direction);   // acesso à 8080
    }

    [Fact]
    public void ParseNetstat_ReadsWindowsOutput()
    {
        const string sample =
            "\r\nConexões ativas\r\n\r\n  Proto  Endereço local          Endereço externo       Estado          PID\r\n" +
            "  TCP    0.0.0.0:135            0.0.0.0:0              LISTENING       1012\r\n" +
            "  TCP    192.168.0.20:3389      192.168.0.9:50001      ESTABLISHED     4321\r\n" +
            "  TCP    [::]:445               [::]:0                 LISTENING       4\r\n" +
            "  UDP    0.0.0.0:123            *:*                                    1500\r\n";

        var list = ConnectionMonitorService.Classify(ConnectionMonitorService.ParseNetstat(sample));

        Assert.Equal(4, list.Count);
        Assert.Equal("LISTEN", list[0].State);
        Assert.Equal(1012, list[0].ProcessId);
        Assert.Equal("ESTAB", list[1].State);
        Assert.Equal("192.168.0.9", list[1].RemoteAddress);
        Assert.Equal(1500, list[3].ProcessId);
        Assert.Equal(ConnectionDirection.Listening, list[3].Direction);
    }

    [Theory]
    [InlineData("192.168.0.1:80", "192.168.0.1", 80)]
    [InlineData("[::1]:631", "::1", 631)]
    [InlineData("*:22", "*", 22)]
    [InlineData("0.0.0.0:*", "0.0.0.0", 0)]
    [InlineData("[fe80::1%eth0]:5353", "fe80::1", 5353)]
    public void TrySplitEndpoint_HandlesIpv4Ipv6AndWildcards(string text, string ip, int port)
    {
        Assert.True(ConnectionMonitorService.TrySplitEndpoint(text, out var a, out var p));
        Assert.Equal(ip, a);
        Assert.Equal(port, p);
    }
}
