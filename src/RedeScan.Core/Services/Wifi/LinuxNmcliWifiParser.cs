using System.Text.RegularExpressions;
using RedeScan.Core.Models;

namespace RedeScan.Core.Services.Wifi;

public static class LinuxNmcliWifiParser
{
    /// <summary>
    /// Faz o parse da saída de:
    /// nmcli -t -f IN-USE,SSID,BSSID,CHAN,FREQ,SIGNAL,SECURITY dev wifi
    /// O nmcli usa backslash para escapar dois pontos (ex: \:), especialmente em BSSIDs.
    /// </summary>
    public static List<WifiNetworkInfo> Parse(string output)
    {
        var list = new List<WifiNetworkInfo>();
        if (string.IsNullOrWhiteSpace(output))
            return list;

        var lines = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

        foreach (var line in lines)
        {
            var tokens = SplitNmcliLine(line);
            if (tokens.Count < 6)
                continue;

            var inUse = tokens[0].Trim() == "*";
            var ssid = tokens[1].Trim();
            var bssid = tokens[2].Trim();
            
            _ = int.TryParse(tokens[3].Trim(), out var channel);

            // FREQ costuma vir como "2412 MHz" ou apenas "2412"
            var freqStr = Regex.Match(tokens[4], @"\d+").Value;
            _ = int.TryParse(freqStr, out var freqMhz);

            // SIGNAL é número de 0 a 100
            var signalStr = Regex.Match(tokens[5], @"\d+").Value;
            _ = int.TryParse(signalStr, out var signalPercent);

            var security = tokens.Count > 6 && !string.IsNullOrWhiteSpace(tokens[6])
                ? tokens[6].Trim()
                : "Open";

            if (string.IsNullOrWhiteSpace(ssid) && string.IsNullOrWhiteSpace(bssid))
                continue;

            list.Add(new WifiNetworkInfo
            {
                Ssid = string.IsNullOrEmpty(ssid) ? "<Oculta>" : ssid,
                Bssid = bssid,
                Channel = channel,
                FrequencyMhz = freqMhz,
                SignalStrengthPercent = Math.Clamp(signalPercent, 0, 100),
                SignalRssiDbm = WifiNetworkInfo.PercentToRssiDbm(signalPercent),
                Security = security,
                IsConnected = inUse
            });
        }

        return list;
    }

    private static List<string> SplitNmcliLine(string line)
    {
        var tokens = new List<string>();
        var current = new System.Text.StringBuilder();
        bool escaping = false;

        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];

            if (escaping)
            {
                current.Append(c);
                escaping = false;
                continue;
            }

            if (c == '\\')
            {
                escaping = true;
                continue;
            }

            if (c == ':')
            {
                tokens.Add(current.ToString());
                current.Clear();
                continue;
            }

            current.Append(c);
        }

        tokens.Add(current.ToString());
        return tokens;
    }
}
