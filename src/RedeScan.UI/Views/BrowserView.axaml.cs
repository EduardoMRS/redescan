using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using RedeScan.UI.Services;
using RedeScan.UI.ViewModels;

namespace RedeScan.UI.Views;

public partial class BrowserView : UserControl
{
    private INativeWebView? _nativeWebView;

    public BrowserView()
    {
        InitializeComponent();
#if ENABLE_NATIVE_MEDIA
        AttachCefWebView();
#else
        AttachNativeFactoryWebView();
#endif
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    /// <summary>
    /// Caminho multiplataforma por fábrica nativa (usado no Android, que fornece
    /// um android.webkit.WebView). Sem fábrica registrada, mantém o fallback.
    /// </summary>
    private void AttachNativeFactoryWebView()
    {
        if (App.NativeWebViewFactory is null)
        {
            return;
        }

        var host = this.FindControl<Panel>("BrowserHost");
        var fallback = this.FindControl<Border>("Fallback");
        if (host is null)
        {
            return;
        }

        try
        {
            _nativeWebView = App.NativeWebViewFactory();
            host.Children.Insert(0, _nativeWebView.Control);
            if (fallback is not null)
            {
                fallback.IsVisible = false;
            }
        }
        catch
        {
            return; // mantém o fallback
        }

        HookViewModel(DataContext as BrowserViewModel, url => _nativeWebView!.Navigate(url));
        DataContextChanged += (_, _) =>
            HookViewModel(DataContext as BrowserViewModel, url => _nativeWebView?.Navigate(url));
    }

    private void HookViewModel(BrowserViewModel? vm, Action<string> navigate)
    {
        if (vm is null)
        {
            return;
        }

        vm.IsWebViewAvailable = true;
        if (!string.IsNullOrWhiteSpace(vm.CurrentUrl))
        {
            navigate(vm.CurrentUrl);
        }

        vm.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(BrowserViewModel.CurrentUrl) &&
                !string.IsNullOrWhiteSpace(vm.CurrentUrl))
            {
                navigate(vm.CurrentUrl);
            }
        };
    }

#if ENABLE_NATIVE_MEDIA
    private WebViewControl.WebView? _webView;

    // Build desktop "Full": WebView real baseado em CEF (WebViewControl-Avalonia).
    private void AttachCefWebView()
    {
        var host = this.FindControl<Panel>("BrowserHost");
        var fallback = this.FindControl<Border>("Fallback");
        if (host is null)
        {
            return;
        }

        try
        {
            _webView = new WebViewControl.WebView();
            host.Children.Insert(0, _webView);
            if (fallback is not null)
            {
                fallback.IsVisible = false;
            }
        }
        catch
        {
            return;
        }

        HookViewModel(DataContext as BrowserViewModel, url => { if (_webView is not null) _webView.Address = url; });
        DataContextChanged += (_, _) =>
            HookViewModel(DataContext as BrowserViewModel, url => { if (_webView is not null) _webView.Address = url; });
    }
#endif
}
