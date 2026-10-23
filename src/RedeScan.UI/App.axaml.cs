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

        // Exceções na thread de UI: registra (arquivo + console) e mantém o app
        // vivo em vez de fechar sem aviso.
        Avalonia.Threading.Dispatcher.UIThread.UnhandledException += (_, e) =>
        {
            RedeScan.Core.Diagnostics.AppLog.WriteCrash("UIThread", e.Exception);
            e.Handled = true;
        };

        Controls.VideoSurface.VideoService = Services.GetService<RedeScan.Core.Services.Media.IVideoService>();

        MainWindowViewModel mainViewModel;
        try
        {
            mainViewModel = Services.GetRequiredService<MainWindowViewModel>();
        }
        catch (Exception ex)
        {
            // Nunca deixe o processo vivo sem janela: registra e segue para
            // abrir ao menos uma janela de erro mínima, em vez de "sumir".
            System.Diagnostics.Trace.TraceError("Falha ao inicializar a UI: " + ex);
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime fallback)
            {
                fallback.MainWindow = new Window
                {
                    Title = "RedeScan — erro na inicialização",
                    Width = 640,
                    Height = 320,
                    Content = new TextBlock
                    {
                        Margin = new Thickness(20),
                        TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                        Text = "O RedeScan não conseguiu iniciar:\n\n" + ex.Message
                    }
                };
                fallback.MainWindow.Show();
            }
            base.OnFrameworkInitializationCompleted();
            return;
        }

        switch (ApplicationLifetime)
        {
            case IClassicDesktopStyleApplicationLifetime desktop:
                _mainWindow = new MainWindow { DataContext = mainViewModel };
                desktop.MainWindow = _mainWindow;

                // Fechar a janela encerra o app (comportamento universal e
                // previsível). O recolhimento para a bandeja acontece ao
                // MINIMIZAR a janela — assim, em ambientes Linux sem uma system
                // tray funcional (ex.: GNOME puro), o usuário nunca fica preso
                // com um processo invisível que não dá para encerrar.
                desktop.ShutdownMode = ShutdownMode.OnMainWindowClose;
                _mainWindow.PropertyChanged += OnMainWindowPropertyChanged;

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
    private void OnMainWindowPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        // Ao minimizar, recolhe para a bandeja escondendo a janela da barra de
        // tarefas. Clicar no ícone da bandeja (OnTrayClicked) a restaura.
        if (e.Property == Window.WindowStateProperty &&
            e.NewValue is WindowState.Minimized &&
            _mainWindow is not null)
        {
            _mainWindow.Hide();
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
