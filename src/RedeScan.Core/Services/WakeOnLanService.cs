using System.Net;
using System.Net.Sockets;

namespace RedeScan.Core.Services;

/// <summary>
/// Implementação de Wake-on-LAN. Monta o magic packet (6 bytes 0xFF seguidos do
/// MAC repetido 16 vezes) e envia por UDP broadcast na porta 9.
/// </summary>
public sealed class WakeOnLanService : IWakeOnLanService
{
    public async Task<bool> WakeAsync(string macAddress, string? broadcastAddress = null, CancellationToken ct = default)
    {
        var mac = ParseMac(macAddress);
        if (mac is null)
        {
            return false;
        }

        // Magic packet: 6x 0xFF + 16x o MAC (6 bytes) = 102 bytes.
        var packet = new byte[6 + 16 * 6];
        for (var i = 0; i < 6; i++)
        {
            packet[i] = 0xFF;
        }
        for (var i = 0; i < 16; i++)
        {
            Array.Copy(mac, 0, packet, 6 + i * 6, 6);
        }

        var broadcast = ResolveBroadcast(broadcastAddress);

        try
        {
            using var udp = new UdpClient { EnableBroadcast = true };
            // Porta 9 (discard) é a convenção para WoL; 7 também é usada.
            await udp.SendAsync(packet, packet.Length, new IPEndPoint(broadcast, 9));
            await udp.SendAsync(packet, packet.Length, new IPEndPoint(broadcast, 7));
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static IPAddress ResolveBroadcast(string? broadcastAddress)
    {
        if (!string.IsNullOrWhiteSpace(broadcastAddress) &&
            IPAddress.TryParse(broadcastAddress, out var addr))
        {
            return addr;
        }
        return IPAddress.Broadcast; // 255.255.255.255
    }

    private static byte[]? ParseMac(string mac)
    {
        if (string.IsNullOrWhiteSpace(mac))
        {
            return null;
        }

        var clean = mac
            .Replace(":", string.Empty)
            .Replace("-", string.Empty)
            .Replace(".", string.Empty)
            .Trim();

        if (clean.Length != 12)
        {
            return null;
        }

        var bytes = new byte[6];
        for (var i = 0; i < 6; i++)
        {
            if (!byte.TryParse(clean.Substring(i * 2, 2), System.Globalization.NumberStyles.HexNumber, null, out bytes[i]))
            {
                return null;
            }
        }
        return bytes;
    }
}
