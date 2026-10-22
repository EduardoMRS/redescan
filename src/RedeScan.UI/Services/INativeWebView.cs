using Avalonia.Controls;

namespace RedeScan.UI.Services;

/// <summary>
/// Abstração de um navegador web nativo embutível. Permite que o head específico
/// de plataforma (ex.: Android, com <c>android.webkit.WebView</c>) forneça o
/// controle real sem que a camada de UI compartilhada conheça as APIs nativas.
/// </summary>
public interface INativeWebView
{
    /// <summary>Controle Avalonia que hospeda o navegador nativo.</summary>
    Control Control { get; }

    /// <summary>Navega o navegador nativo para a URL informada.</summary>
    void Navigate(string url);
}

/// <summary>
/// Fábrica de <see cref="INativeWebView"/>. O head de plataforma registra a
/// implementação em <see cref="RedeScan.UI.App.NativeWebViewFactory"/>; quando
/// nula, a UI cai no comportamento "abrir no navegador do sistema".
/// </summary>
public delegate INativeWebView NativeWebViewFactory();
