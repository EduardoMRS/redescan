using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace RedeScan.Core.Services;

public class NetworkInterfaceService : INetworkInterfaceService
{
    public IPAddress? GetPrimaryLocalIpAddress()
    {
        var interfaces = NetworkInterface.GetAllNetworkInterfaces()
            .Where(ni => ni.OperationalStatus == OperationalStatus.Up &&
                         ni.NetworkInterfaceType != NetworkInterfaceType.Loopback);

        foreach (var ni in interfaces)
        {
            var props = ni.GetIPProperties();
            var ipv4 = props.UnicastAddresses
                .FirstOrDefault(u => u.Address.AddressFamily == AddressFamily.InterNetwork);

            if (ipv4 != null)
                return ipv4.Address;
        }

        return null;
    }

    public IPAddress? GetDefaultGateway()
    {
        var interfaces = NetworkInterface.GetAllNetworkInterfaces()
            .Where(ni => ni.OperationalStatus == OperationalStatus.Up &&
                         ni.NetworkInterfaceType != NetworkInterfaceType.Loopback);

        foreach (var ni in interfaces)
        {
            var props = ni.GetIPProperties();
            var gateway = props.GatewayAddresses
                .FirstOrDefault(g => g.Address.AddressFamily == AddressFamily.InterNetwork);

            if (gateway != null && !gateway.Address.Equals(IPAddress.Any) && !gateway.Address.Equals(IPAddress.None))
                return gateway.Address;
        }

        return null;
    }

    public string? GetSubnetPrefix()
    {
        var localIp = GetPrimaryLocalIpAddress();
        if (localIp == null)
            return null;

        var bytes = localIp.GetAddressBytes();
        return $"{bytes[0]}.{bytes[1]}.{bytes[2]}.";
    }

    public IEnumerable<(string InterfaceName, IPAddress IpAddress, IPAddress? Gateway)> GetAllActiveInterfaces()
    {
        var results = new List<(string, IPAddress, IPAddress?)>();

        var interfaces = NetworkInterface.GetAllNetworkInterfaces()
            .Where(ni => ni.OperationalStatus == OperationalStatus.Up &&
                         ni.NetworkInterfaceType != NetworkInterfaceType.Loopback);

        foreach (var ni in interfaces)
        {
            var props = ni.GetIPProperties();
            var unicast = props.UnicastAddresses
                .FirstOrDefault(u => u.Address.AddressFamily == AddressFamily.InterNetwork);

            if (unicast != null)
            {
                var gateway = props.GatewayAddresses
                    .FirstOrDefault(g => g.Address.AddressFamily == AddressFamily.InterNetwork)?.Address;

                results.Add((ni.Name, unicast.Address, gateway));
            }
        }

        return results;
    }
}
