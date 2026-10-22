using RedeScan.Core.Services;

namespace RedeScan.Core.Tests;

public class NetworkInterfaceServiceTests
{
    [Fact]
    public void GetPrimaryLocalIpAddress_OrGateway_DoesNotThrow()
    {
        var service = new NetworkInterfaceService();

        var localIp = service.GetPrimaryLocalIpAddress();
        var gateway = service.GetDefaultGateway();
        var prefix = service.GetSubnetPrefix();
        var all = service.GetAllActiveInterfaces();

        // Em ambientes conectados, localIp e prefix existirão; o importante é que a lógica não quebre
        Assert.NotNull(all);
        if (localIp != null)
        {
            Assert.NotNull(prefix);
            Assert.EndsWith(".", prefix);
        }
    }
}
