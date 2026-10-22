using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using RedeScan.UI.ViewModels;

namespace RedeScan.UI.Views;

public partial class BrowserView : UserControl
{
    public BrowserView()
    {
        InitializeComponent();
#if ENABLE_NATIVE_MEDIA
        AttachNativeWebView();
#endif
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

#if ENABLE_NATIVE_MEDIA
    private WebViewControl.WebView? _webView;

    // Quando a build habilita mídia nativa, substitui o fallback por um WebView
    // real (WebViewControl-Avalonia / CEF) e sincroniza com CurrentUrl.
    private void AttachNativeWebView()
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
            // Motor web nativo indisponível (ex.: WebKitGTK ausente no Linux):
            // mantém o fallback "abrir no sistema".
            return;
        }

        // Liga aos dados já presentes e às mudanças futuras de DataContext.
        HookViewModel(DataContext as BrowserViewModel);
        DataContextChanged += (_, _) => HookViewModel(DataContext as BrowserViewModel);
    }

    private void HookViewModel(BrowserViewModel? vm)
    {
        if (vm is null || _webView is null)
        {
            return;
        }

        vm.IsWebViewAvailable = true;
        Navigate(vm.CurrentUrl);

        vm.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(BrowserViewModel.CurrentUrl))
            {
                Navigate(vm.CurrentUrl);
            }
        };
    }

    private void Navigate(string? url)
    {
        if (_webView is not null && !string.IsNullOrWhiteSpace(url))
        {
            _webView.Address = url;
        }
    }
#endif
}
