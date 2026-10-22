using Avalonia;
using RedeScan.UI;

namespace RedeScan.Desktop;

internal static class Program
{
    // Ponto de entrada. Não use APIs do Avalonia antes de AppMain ser chamado:
    // tudo que depende de SynchronizationContext inicializado deve ficar depois.
    [STAThread]
    public static void Main(string[] args)
    {
        App.Services = Composition.BuildServiceProvider();
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
