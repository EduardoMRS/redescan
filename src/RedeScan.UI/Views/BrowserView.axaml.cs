using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Avalonia.Platform;
using Avalonia.Threading;
using RedeScan.Core.Diagnostics;
using RedeScan.UI.Services;
using RedeScan.UI.ViewModels;

namespace RedeScan.UI.Views;

/// <summary>
/// Navegador embutido usando o motor web do sistema (sem Chromium empacotado):
/// <list type="bullet">
/// <item>Windows (WebView2) e WPE no Linux: controle embutido (<see cref="NativeWebView"/>).</item>
/// <item>Linux com WebKitGTK: o adaptador só suporta janela própria
/// (<c>SupportedScenarios = NativeDialog</c>), então a página abre num
/// <see cref="NativeWebDialog"/> encaixado sobre a área do navegador e que
/// acompanha a janela principal ao mover/redimensionar.</item>
/// <item>Android: fábrica nativa registrada pelo head.</item>
/// </list>
/// </summary>
public partial class BrowserView : UserControl
{
    private BrowserViewModel? _vm;
    private NativeWebView? _webView;
    private NativeWebDialog? _dialog;
    private INativeWebView? _platformWebView;
    private GLibIdle? _tlsIdle;
    private Window? _ownerWindow;
    private bool _attached;

    public BrowserView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => Bind(DataContext as BrowserViewModel);
        AttachedToVisualTree += (_, _) =>
        {
            _attached = true;
            HookOwnerWindow();
            EnsureEngine();
        };
        DetachedFromVisualTree += (_, _) =>
        {
            _attached = false;
            UnhookOwnerWindow();
            // A janela acoplada não pode ficar flutuando quando o usuário sai da tela.
            CloseDialog();
        };
        Host.SizeChanged += (_, _) => AlignDialog();
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    private Panel Host => this.FindControl<Panel>("BrowserHost")!;

    private void Bind(BrowserViewModel? vm)
    {
        if (_vm is not null)
        {
            _vm.NavigateRequested -= Navigate;
            _vm.ActionRequested -= OnAction;
        }

        _vm = vm;
        if (_vm is not null)
        {
            _vm.NavigateRequested += Navigate;
            _vm.ActionRequested += OnAction;
            EnsureEngine();
        }
    }

    // ------------------------------------------------------------ motor
    private void EnsureEngine()
    {
        if (_vm is null || !_attached || _webView is not null || _dialog is not null || _platformWebView is not null)
        {
            return;
        }

        if (App.NativeWebViewFactory is not null)
        {
            CreatePlatformWebView();
            return;
        }

        var (reason, embeddable) = CheckDesktopEngine();
        if (reason is not null)
        {
            SetUnavailable(reason);
            return;
        }

        if (embeddable)
        {
            CreateEmbedded();
        }
        else
        {
            CreateDialog();
        }
    }

    private void CreatePlatformWebView()
    {
        try
        {
            _platformWebView = App.NativeWebViewFactory!();
            Host.Children.Insert(0, _platformWebView.Control);
            _vm!.IsWebViewAvailable = true;
            _platformWebView.Navigate(_vm.CurrentUrl);
        }
        catch (Exception ex)
        {
            AppLog.Error("WebView da plataforma falhou", ex);
            SetUnavailable("Não foi possível iniciar o WebView do sistema: " + ex.Message);
        }
    }

    private void CreateEmbedded()
    {
        try
        {
            var web = new NativeWebView();
            web.EnvironmentRequested += OnEnvironmentRequested;
            web.AdapterCreated += OnAdapterCreated;
            web.NavigationStarted += OnNavigationStarted;
            web.NavigationCompleted += OnNavigationCompleted;
            web.NewWindowRequested += OnNewWindowRequested;
            _webView = web;
            Host.Children.Insert(0, web);
            _vm!.IsWebViewAvailable = true;
            _vm.IsWindowMode = false;
            Navigate(_vm.CurrentUrl);
        }
        catch (Exception ex)
        {
            AppLog.Error("Falha ao criar o navegador embutido", ex);
            DestroyEmbedded();
            SetUnavailable("O motor web do sistema não iniciou: " + ex.Message);
        }
    }

    private void CreateDialog()
    {
        try
        {
            var dlg = new NativeWebDialog { Title = "RedeScan — Navegador", CanUserResize = true };
            dlg.EnvironmentRequested += OnEnvironmentRequested;
            dlg.AdapterCreated += OnAdapterCreated;
            dlg.NavigationStarted += OnNavigationStarted;
            dlg.NavigationCompleted += OnNavigationCompleted;
            dlg.NewWindowRequested += OnNewWindowRequested;
            dlg.Closing += OnDialogClosing;
            _dialog = dlg;

            if (TopLevel.GetTopLevel(this) is { } top)
            {
                dlg.Show(top); // fica à frente da janela principal
            }
            else
            {
                dlg.Show();
            }

            _vm!.IsWebViewAvailable = true;
            _vm.IsWindowMode = true;
            _vm.IsWindowOpen = true;
            AlignDialog();
            Navigate(_vm.CurrentUrl);
        }
        catch (Exception ex)
        {
            AppLog.Error("Falha ao abrir a janela do navegador", ex);
            CloseDialog();
            SetUnavailable("O motor web do sistema não iniciou: " + ex.Message);
        }
    }

    /// <summary>
    /// Confere o motor do sistema. Devolve o motivo se estiver indisponível e se
    /// ele pode ser embutido no layout (senão, usa janela acoplada).
    /// </summary>
    private static (string? Reason, bool Embeddable) CheckDesktopEngine()
    {
        const WebViewEmbeddingScenario embedded =
            WebViewEmbeddingScenario.NativeControlHost | WebViewEmbeddingScenario.OffscreenRenderer;
        try
        {
            if (OperatingSystem.IsWindows())
            {
                var info = WebViewAdapterInfo.GetAdapterInfo(WebViewAdapterType.WebView2);
                return info.IsInstalled
                    ? (null, true)
                    : ("O Microsoft Edge WebView2 Runtime não está instalado. O instalador Full do RedeScan o instala; " +
                       "na versão portátil, baixe em https://go.microsoft.com/fwlink/p/?LinkId=2124703.", false);
            }

            if (OperatingSystem.IsLinux())
            {
                var wpe = WebViewAdapterInfo.GetAdapterInfo(WebViewAdapterType.WpeWebKit);
                if (wpe.IsInstalled && (wpe.SupportedScenarios & embedded) != 0)
                {
                    return (null, true);
                }

                var gtk = WebViewAdapterInfo.GetAdapterInfo(WebViewAdapterType.WebKitGtk);
                if (gtk.IsInstalled)
                {
                    return (null, (gtk.SupportedScenarios & embedded) != 0);
                }

                return ("O WebKitGTK não está instalado. Instale com:\n" +
                        "Ubuntu/Mint/Debian: sudo apt install libwebkit2gtk-4.1-0\n" +
                        "Fedora: sudo dnf install webkit2gtk4.1\n" +
                        "(os pacotes .deb/.rpm do RedeScan já instalam automaticamente)", false);
            }

            return (null, true); // macOS: WKWebView sempre presente e embutível
        }
        catch (Exception ex)
        {
            AppLog.Warn($"Detecção do motor web falhou: {ex.Message}");
            return (null, true);
        }
    }

    private void SetUnavailable(string reason)
    {
        if (_vm is not null)
        {
            _vm.IsWebViewAvailable = false;
            _vm.IsWindowMode = false;
            _vm.UnavailableReason = reason;
        }

        AppLog.Warn("Navegador embutido indisponível: " + reason.Replace('\n', ' '));
    }

    private void DestroyEmbedded()
    {
        if (_webView is null)
        {
            return;
        }

        _webView.EnvironmentRequested -= OnEnvironmentRequested;
        _webView.AdapterCreated -= OnAdapterCreated;
        _webView.NavigationStarted -= OnNavigationStarted;
        _webView.NavigationCompleted -= OnNavigationCompleted;
        _webView.NewWindowRequested -= OnNewWindowRequested;
        Host.Children.Remove(_webView);
        _webView = null;
    }

    private void CloseDialog()
    {
        var dlg = _dialog;
        _dialog = null;
        if (dlg is null)
        {
            return;
        }

        dlg.Closing -= OnDialogClosing;
        dlg.EnvironmentRequested -= OnEnvironmentRequested;
        dlg.AdapterCreated -= OnAdapterCreated;
        dlg.NavigationStarted -= OnNavigationStarted;
        dlg.NavigationCompleted -= OnNavigationCompleted;
        dlg.NewWindowRequested -= OnNewWindowRequested;
        try
        {
            dlg.Close();
            dlg.Dispose();
        }
        catch (Exception ex)
        {
            AppLog.Warn("Erro ao fechar a janela do navegador: " + ex.Message);
        }

        if (_vm is not null)
        {
            _vm.IsWindowOpen = false;
        }
    }

    /// <summary>O usuário fechou a janela do navegador pelo "X".</summary>
    private void OnDialogClosing(object? sender, EventArgs e)
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (ReferenceEquals(sender, _dialog))
            {
                _dialog = null;
                if (_vm is not null)
                {
                    _vm.IsWindowOpen = false;
                    _vm.IsLoading = false;
                }
            }
        });
    }

    // ----------------------------------------- janela acoplada (Linux)
    private void HookOwnerWindow()
    {
        _ownerWindow = TopLevel.GetTopLevel(this) as Window;
        if (_ownerWindow is not null)
        {
            _ownerWindow.PositionChanged += OnOwnerMoved;
            _ownerWindow.PropertyChanged += OnOwnerPropertyChanged;
        }
    }

    private void UnhookOwnerWindow()
    {
        if (_ownerWindow is not null)
        {
            _ownerWindow.PositionChanged -= OnOwnerMoved;
            _ownerWindow.PropertyChanged -= OnOwnerPropertyChanged;
            _ownerWindow = null;
        }
    }

    private void OnOwnerMoved(object? sender, PixelPointEventArgs e) => AlignDialog();

    private void OnOwnerPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == Window.WindowStateProperty)
        {
            // Minimizou a janela principal: fecha a do navegador junto; ao
            // restaurar, reabre na mesma URL.
            if (e.NewValue is WindowState.Minimized)
            {
                CloseDialog();
            }
            else if (_vm?.IsWindowMode == true && _dialog is null && _attached)
            {
                CreateDialog();
            }
            else
            {
                AlignDialog();
            }
        }
    }

    /// <summary>Encaixa a janela do navegador exatamente sobre a área do navegador no app.</summary>
    private void AlignDialog()
    {
        if (_dialog is null || !_attached || Host.Bounds.Width < 10 || Host.Bounds.Height < 10)
        {
            return;
        }

        Dispatcher.UIThread.Post(() =>
        {
            try
            {
                if (_dialog is null || TopLevel.GetTopLevel(this) is not { } top)
                {
                    return;
                }

                var origin = Host.PointToScreen(new Point(0, 0));
                var scale = top.RenderScaling;
                _dialog.Move(origin.X, origin.Y);
                _dialog.Resize((int)(Host.Bounds.Width * scale), (int)(Host.Bounds.Height * scale));
            }
            catch (Exception ex)
            {
                AppLog.Debug("Não foi possível alinhar a janela do navegador: " + ex.Message);
            }
        }, DispatcherPriority.Background);
    }

    // ------------------------------------------------------ ambiente
    private void OnEnvironmentRequested(object? sender, WebViewEnvironmentRequestedEventArgs e)
    {
        var ignoreCerts = _vm?.IgnoreCertificateErrors == true;
        var data = Path.Combine(AppPaths.UserDataDir, "webview");
        switch (e)
        {
            case WindowsWebView2EnvironmentRequestedEventArgs w2:
                w2.UserDataFolder = data;
                if (ignoreCerts)
                {
                    w2.AdditionalBrowserArguments = "--ignore-certificate-errors";
                }

                break;
            case GtkWebViewEnvironmentRequestedEventArgs gtk:
                gtk.BaseDataDirectory = data;
                gtk.BaseCacheDirectory = Path.Combine(data, "cache");
                break;
        }
    }

    private void OnAdapterCreated(object? sender, WebViewAdapterEventArgs e)
    {
        var info = _webView?.AdapterInfo;
        AppLog.Info($"Navegador: {(_dialog is not null ? "janela acoplada" : "embutido")} {info}");

        var handle = _webView?.TryGetPlatformHandle() ?? _dialog?.TryGetWebViewPlatformHandle();
        if (_vm?.IgnoreCertificateErrors == true && handle is IGtkWebViewPlatformHandle gtk)
        {
            // A política de TLS do WebKitGTK é aplicada na thread do GTK: o
            // g_idle_add agenda a chamada no loop principal do GLib.
            _tlsIdle = GLibIdle.Schedule(() => WebKitGtk.IgnoreTlsErrors(gtk.WebKitWebView));
        }
    }

    // ------------------------------------------------------ navegação
    private void Navigate(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return;
        }

        if (_platformWebView is not null)
        {
            _platformWebView.Navigate(url);
            return;
        }

        if (_webView is null && _dialog is null)
        {
            EnsureEngine(); // já navega para CurrentUrl ao criar
            return;
        }

        try
        {
            var uri = new Uri(url);
            if (_webView is not null)
            {
                _webView.Navigate(uri);
            }
            else
            {
                _dialog!.Navigate(uri);
                AlignDialog();
            }
        }
        catch (Exception ex)
        {
            if (_vm is not null)
            {
                _vm.Status = "URL inválida: " + ex.Message;
            }
        }
    }

    private void OnAction(BrowserAction action)
    {
        switch (action)
        {
            case BrowserAction.Recreate:
                DestroyEmbedded();
                CloseDialog();
                EnsureEngine();
                return;
            case BrowserAction.ShowWindow:
                if (_dialog is null)
                {
                    EnsureEngine();
                }
                else
                {
                    AlignDialog();
                }

                return;
        }

        if (_webView is not null)
        {
            switch (action)
            {
                case BrowserAction.Back when _webView.CanGoBack: _webView.GoBack(); break;
                case BrowserAction.Forward when _webView.CanGoForward: _webView.GoForward(); break;
                case BrowserAction.Reload: _webView.Refresh(); break;
            }
        }
        else if (_dialog is not null)
        {
            switch (action)
            {
                case BrowserAction.Back when _dialog.CanGoBack: _dialog.GoBack(); break;
                case BrowserAction.Forward when _dialog.CanGoForward: _dialog.GoForward(); break;
                case BrowserAction.Reload: _dialog.Refresh(); break;
            }
        }
    }

    private Task<string?> InvokeScriptAsync(string script)
    {
        if (_webView is not null)
        {
            return _webView.InvokeScript(script);
        }

        return _dialog is not null ? _dialog.InvokeScript(script) : Task.FromResult<string?>(null);
    }

    private void OnNavigationStarted(object? sender, WebViewNavigationStartingEventArgs e)
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (_vm is null)
            {
                return;
            }

            _vm.IsLoading = true;
            if (e.Request is { } uri)
            {
                _vm.ReportNavigated(uri.ToString());
                _vm.Status = "Carregando " + uri.Host + "…";
            }
        });
    }

    private void OnNavigationCompleted(object? sender, WebViewNavigationCompletedEventArgs e)
    {
        Dispatcher.UIThread.Post(async () =>
        {
            if (_vm is null)
            {
                return;
            }

            _vm.IsLoading = false;
            if (e.Request is { } uri)
            {
                _vm.ReportNavigated(uri.ToString());
            }

            _vm.Status = e.IsSuccess ? string.Empty : "Falha ao carregar a página.";

            // O WebKit sinaliza "sucesso" mesmo na página de erro de certificado:
            // lê o texto da página para detectar e oferecer a solução.
            if (e.Request?.Scheme == "https" && !_vm.IgnoreCertificateErrors)
            {
                try
                {
                    var text = await InvokeScriptAsync("document.body ? document.body.innerText.substring(0, 300) : ''");
                    if (text is not null &&
                        (text.Contains("TLS certificate", StringComparison.OrdinalIgnoreCase) ||
                         text.Contains("NET::ERR_CERT", StringComparison.OrdinalIgnoreCase) ||
                         (text.Contains("certificate", StringComparison.OrdinalIgnoreCase) && text.Length < 200)))
                    {
                        _vm.ShowCertificateHint = true;
                    }
                }
                catch
                {
                    // script indisponível nesta página
                }
            }
        });
    }

    /// <summary>Links que abririam nova janela abrem na mesma aba do navegador.</summary>
    private void OnNewWindowRequested(object? sender, WebViewNewWindowRequestedEventArgs e)
    {
        if (e.Request is { } uri)
        {
            e.Handled = true;
            Dispatcher.UIThread.Post(() => _vm?.OpenInInternalBrowser(uri.ToString()));
        }
    }

    private void OnAddressKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && _vm is not null)
        {
            _vm.NavigateCommand.Execute(null);
            e.Handled = true;
        }
    }

    // --------------------------------------------- interop WebKitGTK (Linux)
    private static class WebKitGtk
    {
        private const string Lib = "libwebkit2gtk-4.1.so.0";

        [DllImport(Lib)]
        private static extern IntPtr webkit_web_view_get_website_data_manager(IntPtr view);

        [DllImport(Lib)]
        private static extern void webkit_website_data_manager_set_tls_errors_policy(IntPtr manager, int policy);

        /// <summary>WEBKIT_TLS_ERRORS_POLICY_IGNORE = 0.</summary>
        public static void IgnoreTlsErrors(IntPtr webView)
        {
            try
            {
                var manager = webkit_web_view_get_website_data_manager(webView);
                if (manager != IntPtr.Zero)
                {
                    webkit_website_data_manager_set_tls_errors_policy(manager, 0);
                }
            }
            catch (Exception ex)
            {
                AppLog.Warn("Não foi possível ajustar a política TLS do WebKitGTK: " + ex.Message);
            }
        }
    }

    /// <summary>Agenda uma ação única no loop principal do GLib (thread do GTK).</summary>
    private sealed class GLibIdle
    {
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int SourceFunc(IntPtr data);

        [DllImport("libglib-2.0.so.0")]
        private static extern uint g_idle_add(SourceFunc function, IntPtr data);

        // Mantém o delegate vivo enquanto o GLib puder chamá-lo.
        private readonly SourceFunc _callback;

        private GLibIdle(Action action)
        {
            _callback = _ =>
            {
                action();
                return 0; // G_SOURCE_REMOVE
            };
        }

        public static GLibIdle? Schedule(Action action)
        {
            try
            {
                var idle = new GLibIdle(action);
                g_idle_add(idle._callback, IntPtr.Zero);
                return idle;
            }
            catch (Exception ex)
            {
                AppLog.Warn("GLib indisponível: " + ex.Message);
                return null;
            }
        }
    }
}
