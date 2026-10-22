using System.Globalization;
using Avalonia.Data.Converters;

namespace RedeScan.UI.Converters;

/// <summary>
/// Converte uma contagem de redes (int) em altura de barra (double, px) para o
/// gráfico de ocupação de canais Wi-Fi. Cada unidade equivale a 26px, com um
/// mínimo visível para contagens pequenas.
/// </summary>
public sealed class CountToHeightConverter : IValueConverter
{
    private const double PixelsPerUnit = 26.0;
    private const double MinHeight = 8.0;

    public static readonly CountToHeightConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var count = value switch
        {
            int i => i,
            long l => (int)l,
            double d => (int)d,
            _ => 0
        };

        return Math.Max(MinHeight, count * PixelsPerUnit);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
