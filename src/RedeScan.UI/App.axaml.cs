using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using RedeScan.UI.ViewModels;
using RedeScan.UI.Views;

namespace RedeScan.UI;

public partial class App : Application
{
    /// <summary>
    /// Provedor de serviços usado para resolver ViewModels. É atribuído pelo
    /// projeto executável (RedeScan.Desktop) antes da inicialização.
    /// </summary>
    public static IServiceProvider? Services { get; set; }

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        // Evita o aviso/duplicidade do validador de dados do Avalonia junto
        // com as validações do CommunityToolkit.Mvvm.
        DisableAvaloniaDataAnnotationValidation();

        // Se nenhum head configurou o container (ex.: Android), monta o padrão.
        Services ??= Composition.BuildServiceProvider();

        var mainViewModel = Services.GetRequiredService<MainWindowViewModel>();

        switch (ApplicationLifetime)
        {
            case IClassicDesktopStyleApplicationLifetime desktop:
                desktop.MainWindow = new MainWindow { DataContext = mainViewModel };
                break;

            // Android/iOS e demais plataformas de view única.
            case ISingleViewApplicationLifetime singleView:
                singleView.MainView = new MainView { DataContext = mainViewModel };
                break;
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static void DisableAvaloniaDataAnnotationValidation()
    {
        var pluginsToRemove = BindingPlugins.DataValidators
            .OfType<DataAnnotationsValidationPlugin>()
            .ToArray();

        foreach (var plugin in pluginsToRemove)
        {
            BindingPlugins.DataValidators.Remove(plugin);
        }
    }
}
