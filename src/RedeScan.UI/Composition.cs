using Microsoft.Extensions.DependencyInjection;
using RedeScan.Core.Extensions;
using RedeScan.UI.Services;
using RedeScan.UI.ViewModels;

namespace RedeScan.UI;

/// <summary>
/// Raiz de composição compartilhada por todos os heads (Desktop, Android, …).
/// Registra os serviços do Core, os serviços de UI e os ViewModels.
/// </summary>
public static class Composition
{
    public static IServiceProvider BuildServiceProvider(string? customConfigPath = null)
    {
        var services = new ServiceCollection();

        // Serviços de rede/configuração do Core (Fases 1 e 2).
        services.AddRedeScanCore(customConfigPath);

        // Serviços da camada de UI.
        services.AddSingleton<ISystemBrowserLauncher, SystemBrowserLauncher>();
        services.AddSingleton<IBackgroundMonitor, BackgroundMonitor>();

        // ViewModels da camada de apresentação (Fase 3).
        services.AddSingleton<MainWindowViewModel>();
        services.AddSingleton<DashboardViewModel>();
        services.AddSingleton<NetworkScannerViewModel>();
        services.AddSingleton<DiagnosticsViewModel>();
        services.AddSingleton<WifiScannerViewModel>();
        services.AddSingleton<CamerasViewModel>();
        services.AddSingleton<ShortcutsViewModel>();
        services.AddSingleton<BrowserViewModel>();
        services.AddSingleton<NetworkConfigViewModel>();
        services.AddSingleton<TerminalViewModel>();
        services.AddSingleton<TftpViewModel>();

        // IAppNavigator é o próprio MainWindowViewModel (resolvido sob demanda
        // para evitar ciclo com os ViewModels que o consomem).
        services.AddSingleton<IAppNavigator>(sp => sp.GetRequiredService<MainWindowViewModel>());

        return services.BuildServiceProvider();
    }
}
