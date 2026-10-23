using Microsoft.Extensions.DependencyInjection;
using RedeScan.Core.Services;
using RedeScan.Core.Services.Media;
using RedeScan.Core.Services.Wifi;

namespace RedeScan.Core.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddRedeScanCore(this IServiceCollection services, string? customConfigPath = null)
    {
        services.AddSingleton<INetworkInterfaceService, NetworkInterfaceService>();
        services.AddSingleton<IPingService, PingService>();
        services.AddSingleton<IArpService, ArpService>();
        services.AddSingleton<IOuiVendorLookup, OuiVendorLookup>();
        services.AddSingleton<INetworkScannerService, NetworkScannerService>();
        services.AddSingleton<IWebDeviceScanner, WebDeviceScanner>();
        services.AddSingleton<INetworkConfigurationService, NetworkConfigurationService>();
        services.AddSingleton<IHostInspector, HostInspector>();
        services.AddSingleton<IWakeOnLanService, WakeOnLanService>();
        services.AddSingleton<IRtspListImporter, RtspListImporter>();
        services.AddSingleton<IExternalToolService, ExternalToolService>();
        services.AddSingleton<IVideoService, FfmpegVideoService>();
        services.AddSingleton<IConnectionMonitorService, ConnectionMonitorService>();
        services.AddSingleton<IRemoteAccessService, RemoteAccessService>();
        services.AddSingleton<ISshTerminalFactory, SshTerminalFactory>();
        services.AddSingleton<ITftpServer, TftpServer>();
        services.AddSingleton<IUpdateService>(_ => new UpdateService());
        services.AddSingleton<IPortScannerService, PortScannerService>();
        services.AddSingleton<ILoadTestService, LoadTestService>();
        services.AddSingleton<IConfigurationService>(_ => new ConfigurationService(customConfigPath));
        services.AddSingleton<IWifiScanner, PlatformWifiScanner>();

        return services;
    }
}
