using RedeScan.Core.Models;
using RedeScan.Core.Services;
using RedeScan.Core.Services.Wifi;

namespace RedeScan.Core.Tests;

public class WifiParserTests
{
    [Fact]
    public void LinuxNmcliWifiParser_ParsesValidOutputWithEscapedColons()
    {
        var sampleOutput = 
            "*:MinhaRede_5G:AA\\:BB\\:CC\\:DD\\:EE\\:01:36:5180 MHz:88:WPA2 WPA3\n" +
            " :Vizinho_2.4G:11\\:22\\:33\\:44\\:55\\:66:6:2437 MHz:62:WPA2\n" +
            " ::00\\:11\\:22\\:33\\:44\\:55:1:2412 MHz:40:WPA2"; // Rede oculta

        var networks = LinuxNmcliWifiParser.Parse(sampleOutput);

        Assert.Equal(3, networks.Count);

        // Rede 1 (Conectada, 5G)
        Assert.True(networks[0].IsConnected);
        Assert.Equal("MinhaRede_5G", networks[0].Ssid);
        Assert.Equal("AA:BB:CC:DD:EE:01", networks[0].Bssid);
        Assert.Equal(36, networks[0].Channel);
        Assert.Equal(5180, networks[0].FrequencyMhz);
        Assert.Equal("5 GHz", networks[0].Band);
        Assert.Equal(88, networks[0].SignalStrengthPercent);
        Assert.Equal(-56, networks[0].SignalRssiDbm);
        Assert.Equal("WPA2 WPA3", networks[0].Security);

        // Rede 2 (2.4G)
        Assert.False(networks[1].IsConnected);
        Assert.Equal("Vizinho_2.4G", networks[1].Ssid);
        Assert.Equal("11:22:33:44:55:66", networks[1].Bssid);
        Assert.Equal(6, networks[1].Channel);
        Assert.Equal(2437, networks[1].FrequencyMhz);
        Assert.Equal("2.4 GHz", networks[1].Band);
        Assert.Equal(62, networks[1].SignalStrengthPercent);

        // Rede 3 (Oculta)
        Assert.Equal("<Oculta>", networks[2].Ssid);
        Assert.Equal(1, networks[2].Channel);
        Assert.Equal("2.4 GHz", networks[2].Band);
    }

    [Fact]
    public void WindowsNetshWifiParser_ParsesValidOutput()
    {
        var sampleOutput = @"
Interface name : Wi-Fi 
There are 2 networks currently visible. 

SSID 1 : Office_Network
    Network type            : Infrastructure
    Authentication          : WPA2-Personal
    Encryption              : CCMP 
    BSSID 1                 : 00:1a:2b:3c:4d:5e
         Signal             : 75%  
         Radio type         : 802.11ac
         Channel            : 44 
         Basic rates (Mbps) : 6 12 24
         Other rates (Mbps) : 9 18 36 48 54

SSID 2 : Guest_WiFi
    Network type            : Infrastructure
    Authentication          : Open
    Encryption              : None
    BSSID 1                 : aa:bb:cc:dd:ee:ff
         Signal             : 90%  
         Radio type         : 802.11ax
         Channel            : 6 
";

        var networks = WindowsNetshWifiParser.Parse(sampleOutput);

        Assert.Equal(2, networks.Count);

        Assert.Equal("Office_Network", networks[0].Ssid);
        Assert.Equal("00:1a:2b:3c:4d:5e", networks[0].Bssid);
        Assert.Equal(44, networks[0].Channel);
        Assert.Equal(5220, networks[0].FrequencyMhz);
        Assert.Equal("5 GHz", networks[0].Band);
        Assert.Equal(75, networks[0].SignalStrengthPercent);
        Assert.Equal("WPA2-Personal", networks[0].Security);

        Assert.Equal("Guest_WiFi", networks[1].Ssid);
        Assert.Equal(6, networks[1].Channel);
        Assert.Equal(2437, networks[1].FrequencyMhz);
        Assert.Equal("2.4 GHz", networks[1].Band);
        Assert.Equal(90, networks[1].SignalStrengthPercent);
        Assert.Equal("Open", networks[1].Security);
    }

    [Fact]
    public void WifiNetworkInfo_CalculatesRssiAndBandsAccurately()
    {
        // 100% -> -50 dBm
        Assert.Equal(-50, WifiNetworkInfo.PercentToRssiDbm(100));
        // 0% -> -100 dBm
        Assert.Equal(-100, WifiNetworkInfo.PercentToRssiDbm(0));
        // 50% -> -75 dBm
        Assert.Equal(-75, WifiNetworkInfo.PercentToRssiDbm(50));

        var net24 = new WifiNetworkInfo { Channel = 11, FrequencyMhz = 2462 };
        Assert.Equal("2.4 GHz", net24.Band);

        var net5 = new WifiNetworkInfo { Channel = 149, FrequencyMhz = 5745 };
        Assert.Equal("5 GHz", net5.Band);

        var net6 = new WifiNetworkInfo { Channel = 1, FrequencyMhz = 5955 };
        Assert.Equal("6 GHz", net6.Band);
    }

    [Fact]
    public async Task PlatformWifiScanner_ExecutionDoesNotThrow()
    {
        var scanner = new PlatformWifiScanner();

        Assert.NotNull(scanner.PlatformName);
        var isSupported = await scanner.IsSupportedAsync();
        var networks = await scanner.GetAvailableNetworksAsync();

        Assert.NotNull(networks);
        // Mesmo em máquina sem Wi-Fi, o método deve retornar lista vazia de forma segura
    }
}
