using System.Text.RegularExpressions;
using RedeScan.Core.Models;

namespace RedeScan.Core.Services.Wifi;

public static class WindowsNetshWifiParser
{
    public static List<WifiNetworkInfo> Parse(string output)
    {
        var list = new List<WifiNetworkInfo>();
        if (string.IsNullOrWhiteSpace(output))
            return list;

        var lines = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

        string currentSsid = string.Empty;
        string currentAuth = "Open";

        foreach (var line in lines)
        {
            var trimmed = line.Trim();

            var ssidMatch = Regex.Match(trimmed, @"^SSID\s+\d+\s*:\s*(.*)$", RegexOptions.IgnoreCase);
            if (ssidMatch.Success)
            {
                currentSsid = ssidMatch.Groups[1].Value.Trim();
                continue;
            }

            var authMatch = Regex.Match(trimmed, @"^Autentica[çc][ãa]o\s*:\s*(.*)$|^Authentication\s*:\s*(.*)$", RegexOptions.IgnoreCase);
            if (authMatch.Success)
            {
                currentAuth = (authMatch.Groups[1].Success ? authMatch.Groups[1].Value : authMatch.Groups[2].Value).Trim();
                continue;
            }

            var bssidMatch = Regex.Match(trimmed, @"^BSSID\s+\d+\s*:\s*([0-9a-fA-F:]{17})", RegexOptions.IgnoreCase);
            if (bssidMatch.Success)
            {
                var bssid = bssidMatch.Groups[1].Value.Trim();
                // Sublinhas após o BSSID contêm Signal e Channel
                list.Add(new WifiNetworkInfo
                {
                    Ssid = string.IsNullOrEmpty(currentSsid) ? "<Oculta>" : currentSsid,
                    Bssid = bssid,
                    Security = currentAuth,
                    SignalStrengthPercent = 50, // default até achar a linha de sinal
                    Channel = 1
                });
                continue;
            }

            var signalMatch = Regex.Match(trimmed, @"^Sinal\s*:\s*(\d+)%|^Signal\s*:\s*(\d+)%", RegexOptions.IgnoreCase);
            if (signalMatch.Success && list.Count > 0)
            {
                var signalStr = signalMatch.Groups[1].Success ? signalMatch.Groups[1].Value : signalMatch.Groups[2].Value;
                if (int.TryParse(signalStr, out var signal))
                {
                    var last = list[^1];
                    list[^1] = last with
                    {
                        SignalStrengthPercent = signal,
                        SignalRssiDbm = WifiNetworkInfo.PercentToRssiDbm(signal)
                    };
                }
                continue;
            }

            var channelMatch = Regex.Match(trimmed, @"^Canal\s*:\s*(\d+)|^Channel\s*:\s*(\d+)", RegexOptions.IgnoreCase);
            if (channelMatch.Success && list.Count > 0)
            {
                var chanStr = channelMatch.Groups[1].Success ? channelMatch.Groups[1].Value : channelMatch.Groups[2].Value;
                if (int.TryParse(chanStr, out var channel))
                {
                    var last = list[^1];
                    var freq = ChannelToFrequencyMhz(channel);
                    list[^1] = last with
                    {
                        Channel = channel,
                        FrequencyMhz = freq
                    };
                }
                continue;
            }
        }

        return list;
    }

    private static int ChannelToFrequencyMhz(int channel)
    {
        if (channel is >= 1 and <= 14)
        {
            return channel == 14 ? 2484 : 2407 + (channel * 5);
        }
        if (channel is >= 36 and <= 177)
        {
            return 5000 + (channel * 5);
        }
        return 0;
    }
}
