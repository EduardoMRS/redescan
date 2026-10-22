using Android.App;
using Android.Content.PM;
using Avalonia;
using Avalonia.Android;

namespace RedeScan.Android;

[Activity(
    Label = "RedeScan",
    Theme = "@style/MyTheme.NoActionBar",
    Icon = "@drawable/icon",
    MainLauncher = true,
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.UiMode)]
public class MainActivity : AvaloniaMainActivity<global::RedeScan.UI.App>
{
    protected override AppBuilder CustomizeAppBuilder(AppBuilder builder)
    {
        // Registra o navegador nativo do Android para o módulo de navegador
        // integrado renderizar inline (em vez de cair no "abrir no sistema").
        global::RedeScan.UI.App.NativeWebViewFactory = () => new AndroidWebView();

        return base.CustomizeAppBuilder(builder)
            .WithInterFont();
    }
}
