using System.Net;

namespace RedeScan.Core.Services;

public interface INetworkInterfaceService
{
    IPAddress? GetPrimaryLocalIpAddress();
    IPAddress? GetDefaultGateway();
    string? GetSubnetPrefix();
    IEnumerable<(string InterfaceName, IPAddress IpAddress, IPAddress? Gateway)> GetAllActiveInterfaces();
}
