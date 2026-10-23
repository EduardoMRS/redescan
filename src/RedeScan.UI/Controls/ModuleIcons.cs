using System.Globalization;
using Avalonia.Data.Converters;
using Material.Icons;

namespace RedeScan.UI.Controls;

/// <summary>Ícone vetorial de cada módulo, pela chave estável do ViewModel.</summary>
public static class ModuleIcons
{
    private static readonly Dictionary<string, MaterialIconKind> Map = new()
    {
        ["dashboard"] = MaterialIconKind.ViewDashboardOutline,
        ["networkscanner"] = MaterialIconKind.Radar,
        ["diagnostics"] = MaterialIconKind.Stethoscope,
        ["wifiscanner"] = MaterialIconKind.Wifi,
        ["cameras"] = MaterialIconKind.Cctv,
        ["shortcuts"] = MaterialIconKind.LinkVariant,
        ["browser"] = MaterialIconKind.Web,
        ["networkconfig"] = MaterialIconKind.LanPending,
        ["terminal"] = MaterialIconKind.Console,
        ["tftp"] = MaterialIconKind.ServerNetwork,
        ["accessmonitor"] = MaterialIconKind.ShieldSearch,
        ["settings"] = MaterialIconKind.CogOutline
    };

    public static MaterialIconKind For(string key) =>
        Map.TryGetValue(key, out var kind) ? kind : MaterialIconKind.CircleOutline;

    /// <summary>Converte a chave do módulo (string) no ícone, para uso em XAML.</summary>
    public static readonly IValueConverter KeyToIcon =
        new FuncValueConverter<string?, MaterialIconKind>(key => For(key ?? string.Empty));
}
