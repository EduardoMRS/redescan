using Microsoft.Extensions.DependencyInjection;
using RedeScan.Core.Services;

namespace RedeScan.Core.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddRedeScanCore(this IServiceCollection services, string? customConfigPath = null)
    {
        services.AddSingleton<INetworkInterfaceService, NetworkInterfaceService>();
        services.AddSingleton<IPingService, PingService>();
        services.AddSingleton<INetworkScannerService, NetworkScannerService>();
        services.AddSingleton<IPortScannerService, PortScannerService>();
        services.AddSingleton<ILoadTestService, LoadTestService>();
        services.AddSingleton<IConfigurationService>(_ => new ConfigurationService(customConfigPath));

        return services;
    }
}
