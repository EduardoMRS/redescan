using System.Runtime.InteropServices;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Platform;
using Avalonia.Threading;
using RedeScan.Core.Diagnostics;
using RedeScan.UI.Services;
using RedeScan.UI.ViewModels;

namespace RedeScan.UI.Views;

/// <summary>
/// Navegador embutido. No desktop usa o <see cref="NativeWebView"/> do Avalonia
/// (WebView2 no Windows, WebKitGTK no Linux — o motor do próprio sistema, sem
/// embutir Chromium). No Android usa a fábrica nativa registrada pelo head.
/// </summary>
public partial class BrowserView : UserControl
{
    private BrowserViewModel? _vm;
    private NativeWebView? _webView;
    private INativeWebView? _platformWebView;
    private GLibIdle? _tlsIdle;

    public BrowserView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => Bind(DataContext as BrowserViewModel);
        AttachedToVisualTree += (_, _) => EnsureEngine();
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
        if (_vm is null || VisualRoot is null || _webView is not null || _platformWebView is not null)
        {
            return;
        }

        // Android (ou outro head) com WebView nativo próprio.
        if (App.NativeWebViewFactory is not null)
        {
            try
            {
                _platformWebView = App.NativeWebViewFactory();
                Host.Children.Insert(0, _platformWebView.Control);
                _vm.IsWebViewAvailable = true;
                _platformWebView.Navigate(_vm.CurrentUrl);
            }
            catch (Exception ex)
            {
                AppLog.Error("WebView da plataforma falhou", ex);
                SetUnavailable("Não foi possível iniciar o WebView do sistema: " + ex.Message);
            }

            return;
        }

        var reason = CheckDesktopEngine();
        if (reason is not null)
        {
            SetUnavailable(reason);
            return;
        }

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
            _vm.IsWebViewAvailable = true;
            Navigate(_vm.CurrentUrl);
        }
        catch (Exception ex)
        {
            AppLog.Error("Falha ao criar o navegador embutido", ex);
            DestroyEngine();
            SetUnavailable("O motor web do sistema não iniciou: " + ex.Message);
        }
    }

    /// <summary>Confere se o motor do sistema está instalado; devolve o motivo se não.</summary>
    private static string? CheckDesktopEngine()
    {
        try
        {
            if (OperatingSystem.IsWindows())
            {
                var info = WebViewAdapterInfo.GetAdapterInfo(WebViewAdapterType.WebView2);
                return info.IsInstalled
                    ? null
                    : "O Microsoft Edge WebView2 Runtime não está instalado. O instalador Full do RedeScan o instala; " +
                      "na versão portátil, baixe em https://go.microsoft.com/fwlink/p/?LinkId=2124703.";
            }

            if (OperatingSystem.IsLinux())
            {
                var gtk = WebViewAdapterInfo.GetAdapterInfo(WebViewAdapterType.WebKitGtk);
                var wpe = WebViewAdapterInfo.GetAdapterInfo(WebViewAdapterType.WpeWebKit);
                if (gtk.IsInstalled || wpe.IsInstalled)
                {
                    return null;
                }

                return "O WebKitGTK não está instalado. Instale com:\n" +
                       "Ubuntu/Mint/Debian: sudo apt install libwebkit2gtk-4.1-0\n" +
                       "Fedora: sudo dnf install webkit2gtk4.1\n" +
                       "(os pacotes .deb/.rpm do RedeScan já instalam automaticamente)";
            }

            return null; // macOS: WKWebView sempre presente
        }
        catch (Exception ex)
        {
            AppLog.Warn($"Detecção do motor web falhou: {ex.Message}");
            return null; // tenta assim mesmo
        }
    }

    private void SetUnavailable(string reason)
    {
        if (_vm is not null)
        {
            _vm.IsWebViewAvailable = false;
            _vm.UnavailableReason = reason;
        }

        AppLog.Warn("Navegador embutido indisponível: " + reason.Replace('\n', ' '));
    }

    private void DestroyEngine()
    {
        if (_webView is not null)
        {
            _webView.EnvironmentRequested -= OnEnvironmentRequested;
            _webView.AdapterCreated -= OnAdapterCreated;
            _webView.NavigationStarted -= OnNavigationStarted;
            _webView.NavigationCompleted -= OnNavigationCompleted;
            _webView.NewWindowRequested -= OnNewWindowRequested;
            Host.Children.Remove(_webView);
            _webView = null;
        }
    }

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
        AppLog.Info($"Navegador embutido: {_webView?.AdapterInfo}");
        if (_vm?.IgnoreCertificateErrors == true &&
            _webView?.TryGetPlatformHandle() is IGtkWebViewPlatformHandle gtk)
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

        if (_webView is null)
        {
            EnsureEngine();
            return; // EnsureEngine já navega para CurrentUrl
        }

        try
        {
            _webView.Navigate(new Uri(url));
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
        if (action == BrowserAction.Recreate)
        {
            DestroyEngine();
            EnsureEngine();
            return;
        }

        if (_webView is null)
        {
            return;
        }

        switch (action)
        {
            case BrowserAction.Back when _webView.CanGoBack:
                _webView.GoBack();
                break;
            case BrowserAction.Forward when _webView.CanGoForward:
                _webView.GoForward();
                break;
            case BrowserAction.Reload:
                _webView.Refresh();
                break;
        }
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

    private async void OnNavigationCompleted(object? sender, WebViewNavigationCompletedEventArgs e)
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
        if (e.Request?.Scheme == "https" && _webView is not null && !_vm.IgnoreCertificateErrors)
        {
            try
            {
                var text = await _webView.InvokeScript("document.body ? document.body.innerText.substring(0, 300) : ''");
                if (text is not null &&
                    (text.Contains("TLS certificate", StringComparison.OrdinalIgnoreCase) ||
                     text.Contains("NET::ERR_CERT", StringComparison.OrdinalIgnoreCase) ||
                     text.Contains("certificate", StringComparison.OrdinalIgnoreCase) && text.Length < 200))
                {
                    _vm.ShowCertificateHint = true;
                }
            }
            catch
            {
                // script indisponível nesta página
            }
        }
    }

    /// <summary>Links que abririam nova janela abrem na mesma aba do navegador embutido.</summary>
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
