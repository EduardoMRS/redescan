using Avalonia;
using Microsoft.Extensions.DependencyInjection;
using RedeScan.Core.Extensions;
using RedeScan.UI;
using RedeScan.UI.ViewModels;

namespace RedeScan.Desktop;

internal static class Program
{
    // Ponto de entrada. Não use APIs do Avalonia antes de AppMain ser chamado:
    // tudo que depende de SynchronizationContext inicializado deve ficar depois.
    [STAThread]
    public static void Main(string[] args)
    {
        App.Services = BuildServiceProvider();
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();

    private static ServiceProvider BuildServiceProvider()
    {
        var services = new ServiceCollection();

        // Serviços de rede/configuração do Core (Fases 1 e 2).
        services.AddRedeScanCore();

        // ViewModels da camada de apresentação (Fase 3).
        services.AddSingleton<MainWindowViewModel>();
        services.AddSingleton<DashboardViewModel>();
        services.AddSingleton<NetworkScannerViewModel>();
        services.AddSingleton<DiagnosticsViewModel>();
        services.AddSingleton<WifiScannerViewModel>();
        services.AddSingleton<CamerasViewModel>();
        services.AddSingleton<ShortcutsViewModel>();

        return services.BuildServiceProvider();
    }
}
