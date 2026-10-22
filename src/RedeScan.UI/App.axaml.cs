using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using RedeScan.UI.Services;
using RedeScan.UI.ViewModels;
using RedeScan.UI.Views;

namespace RedeScan.UI;

public partial class App : Application
{
    private Window? _mainWindow;
    private IBackgroundMonitor? _monitor;
    /// <summary>
    /// Provedor de serviços usado para resolver ViewModels. É atribuído pelo
    /// projeto executável (RedeScan.Desktop) antes da inicialização.
    /// </summary>
    public static IServiceProvider? Services { get; set; }

    /// <summary>
    /// Fábrica de navegador nativo definida pelo head de plataforma (ex.: o
    /// head Android registra uma que cria um <c>android.webkit.WebView</c>).
    /// Quando nula, o módulo de navegador usa "abrir no sistema".
    /// </summary>
    public static NativeWebViewFactory? NativeWebViewFactory { get; set; }

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
                _mainWindow = new MainWindow { DataContext = mainViewModel };
                desktop.MainWindow = _mainWindow;

                // Fechar a janela recolhe para a bandeja em vez de encerrar.
                desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;
                _mainWindow.Closing += OnMainWindowClosing;

                // Monitor de segundo plano: avisa na bandeja sobre mudanças.
                _monitor = Services.GetService<IBackgroundMonitor>();
                if (_monitor is not null)
                {
                    _monitor.Notified += OnMonitorNotified;
                }
                break;

            // Android/iOS e demais plataformas de view única.
            case ISingleViewApplicationLifetime singleView:
                singleView.MainView = new MainView { DataContext = mainViewModel };
                break;
        }

        base.OnFrameworkInitializationCompleted();
    }

    // -------------------------------------------------- Bandeja (desktop)
    private void OnMainWindowClosing(object? sender, WindowClosingEventArgs e)
    {
        // Em vez de fechar, esconde na bandeja (a menos que seja shutdown real).
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime)
        {
            e.Cancel = true;
            _mainWindow?.Hide();
        }
    }

    private void ShowMainWindow()
    {
        if (_mainWindow is null)
        {
            return;
        }
        _mainWindow.Show();
        _mainWindow.WindowState = WindowState.Normal;
        _mainWindow.Activate();
    }

    private void OnTrayClicked(object? sender, EventArgs e) => ShowMainWindow();

    private void OnTrayOpen(object? sender, EventArgs e) => ShowMainWindow();

    private void OnTrayToggleMonitor(object? sender, EventArgs e)
    {
        if (_monitor is null)
        {
            return;
        }

        if (_monitor.IsRunning)
        {
            _monitor.Stop();
        }
        else
        {
            _monitor.Start();
        }

        if (sender is NativeMenuItem item)
        {
            item.IsChecked = _monitor.IsRunning;
        }
    }

    private void OnTrayExit(object? sender, EventArgs e)
    {
        _monitor?.Stop();
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // Remove o handler para o fechamento não ser cancelado.
            if (_mainWindow is not null)
            {
                _mainWindow.Closing -= OnMainWindowClosing;
            }
            desktop.Shutdown();
        }
    }

    private void OnMonitorNotified(MonitorNotification n)
    {
        // Atualiza o tooltip da bandeja com o último aviso (notificação nativa
        // completa varia muito por plataforma; o tooltip é o denominador comum).
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            var icons = TrayIcon.GetIcons(this);
            if (icons is { Count: > 0 })
            {
                icons[0].ToolTipText = $"RedeScan — {n.Title}: {n.Message}";
            }
        });
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
