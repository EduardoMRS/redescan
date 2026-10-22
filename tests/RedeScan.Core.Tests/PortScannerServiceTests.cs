using System.Net;
using System.Net.Sockets;
using RedeScan.Core.Services;

namespace RedeScan.Core.Tests;

public class PortScannerServiceTests
{
    [Fact]
    public void KnownPorts_ContainsStandardServices()
    {
        var service = new PortScannerService();

        Assert.True(service.KnownPorts.ContainsKey(80));
        Assert.Equal("HTTP", service.KnownPorts[80]);
        Assert.True(service.KnownPorts.ContainsKey(443));
        Assert.Equal("HTTPS", service.KnownPorts[443]);
        Assert.True(service.KnownPorts.ContainsKey(554));
        Assert.Equal("RTSP", service.KnownPorts[554]);
    }

    [Fact]
    public async Task ScanSinglePort_DetectsOpenLocalPort()
    {
        // Abre um TcpListener local temporário para testar porta aberta
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;

        try
        {
            var service = new PortScannerService();
            var result = await service.ScanSinglePortAsync("127.0.0.1", port, timeoutMs: 1000);

            Assert.True(result.IsOpen);
            Assert.Equal(port, result.Port);
            Assert.Equal("127.0.0.1", result.IpAddress);
        }
        finally
        {
            listener.Stop();
        }
    }
}
