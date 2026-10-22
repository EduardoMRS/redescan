using Avalonia.Android;
using Avalonia.Controls;
using Avalonia.Platform;
using RedeScan.UI.Services;

namespace RedeScan.Android;

/// <summary>
/// Navegador web nativo do Android embutido na árvore Avalonia via
/// <see cref="NativeControlHost"/>, envolvendo um <c>android.webkit.WebView</c>.
/// </summary>
public sealed class AndroidWebView : INativeWebView
{
    private readonly AndroidWebViewHost _host = new();

    public Control Control => _host;

    public void Navigate(string url) => _host.Navigate(url);
}

/// <summary>
/// Host que cria e posiciona o WebView nativo. O WebView real só existe depois
/// que o Android chama <see cref="CreateNativeControlCore"/>; navegações
/// pedidas antes disso ficam pendentes e são aplicadas na criação.
/// </summary>
internal sealed class AndroidWebViewHost : NativeControlHost
{
    private global::Android.Webkit.WebView? _webView;
    private string? _pendingUrl;

    public void Navigate(string url)
    {
        if (_webView is not null)
        {
            _webView.LoadUrl(url);
        }
        else
        {
            _pendingUrl = url;
        }
    }

    protected override IPlatformHandle CreateNativeControlCore(IPlatformHandle parent)
    {
        var context = (parent as AndroidViewControlHandle)?.View.Context
                      ?? global::Android.App.Application.Context!;

        var webView = new global::Android.Webkit.WebView(context);
        webView.Settings.JavaScriptEnabled = true;
        webView.Settings.DomStorageEnabled = true;
        // Mantém a navegação dentro do WebView (não abre o navegador externo).
        webView.SetWebViewClient(new global::Android.Webkit.WebViewClient());

        _webView = webView;
        if (!string.IsNullOrWhiteSpace(_pendingUrl))
        {
            webView.LoadUrl(_pendingUrl);
            _pendingUrl = null;
        }

        return new AndroidViewControlHandle(webView);
    }

    protected override void DestroyNativeControlCore(IPlatformHandle control)
    {
        _webView?.Destroy();
        _webView = null;
        base.DestroyNativeControlCore(control);
    }
}
