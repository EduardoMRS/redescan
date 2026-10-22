using System.Diagnostics;
using System.Runtime.InteropServices;

namespace RedeScan.UI.Services;

/// <summary>
/// Implementação multiplataforma que delega ao navegador padrão do SO
/// (<c>start</c>/shell no Windows, <c>xdg-open</c> no Linux, <c>open</c> no macOS).
/// </summary>
public sealed class SystemBrowserLauncher : ISystemBrowserLauncher
{
    public void Open(string target)
    {
        if (string.IsNullOrWhiteSpace(target))
        {
            return;
        }

        var url = NormalizeUrl(target);

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            Process.Start("xdg-open", url);
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            Process.Start("open", url);
        }
    }

    /// <summary>Prefixa <c>http://</c> quando o alvo não traz esquema.</summary>
    public static string NormalizeUrl(string target) =>
        target.Contains("://", StringComparison.Ordinal) ? target : $"http://{target}";
}
