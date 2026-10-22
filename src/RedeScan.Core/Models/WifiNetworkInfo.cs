namespace RedeScan.Core.Models;

public record WifiNetworkInfo
{
    public string Ssid { get; init; } = string.Empty;
    public string Bssid { get; init; } = string.Empty;
    public int SignalStrengthPercent { get; init; }
    public int SignalRssiDbm { get; init; }
    public int Channel { get; init; }
    public int FrequencyMhz { get; init; }
    public string Band => FrequencyMhz switch
    {
        >= 2400 and <= 2500 => "2.4 GHz",
        >= 5000 and <= 5900 => "5 GHz",
        >= 5925 and <= 7125 => "6 GHz",
        _ => Channel switch
        {
            >= 1 and <= 14 => "2.4 GHz",
            >= 32 and <= 177 => "5 GHz",
            _ => "Desconhecida"
        }
    };
    public string Security { get; init; } = "Open";
    public bool IsConnected { get; init; }

    /// <summary>
    /// Converte porcentagem (0-100) para dBm estimado (-100 a -50 dBm).
    /// </summary>
    public static int PercentToRssiDbm(int percent)
    {
        // 0% ~= -100 dBm, 100% ~= -50 dBm
        return (percent / 2) - 100;
    }

    /// <summary>
    /// Converte dBm (-100 a -50) para porcentagem aproximada (0-100).
    /// </summary>
    public static int RssiDbmToPercent(int dbm)
    {
        if (dbm <= -100) return 0;
        if (dbm >= -50) return 100;
        return 2 * (dbm + 100);
    }
}
