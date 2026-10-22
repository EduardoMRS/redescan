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

        var webView = new WebViewControl.WebView();
        host.Children.Insert(0, webView);
        if (fallback is not null)
        {
            fallback.IsVisible = false;
        }

        void Navigate(string? url)
        {
            if (!string.IsNullOrWhiteSpace(url))
            {
                webView.Address = url;
            }
        }

        DataContextChanged += (_, _) =>
        {
            if (DataContext is BrowserViewModel vm)
            {
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
        };
    }
#endif
}
