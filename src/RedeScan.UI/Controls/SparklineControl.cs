using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace RedeScan.UI.Controls;

/// <summary>
/// Gráfico de linha leve e sem dependências externas. Plota uma sequência de
/// valores <see cref="double"/> (ex.: latência de ping ao longo do tempo),
/// escalando automaticamente ao maior valor observado.
/// </summary>
public class SparklineControl : Control
{
    public static readonly StyledProperty<IEnumerable<double>?> ValuesProperty =
        AvaloniaProperty.Register<SparklineControl, IEnumerable<double>?>(nameof(Values));

    public static readonly StyledProperty<IBrush> StrokeProperty =
        AvaloniaProperty.Register<SparklineControl, IBrush>(nameof(Stroke), Brushes.DeepSkyBlue);

    public static readonly StyledProperty<double> StrokeThicknessProperty =
        AvaloniaProperty.Register<SparklineControl, double>(nameof(StrokeThickness), 2.0);

    public IEnumerable<double>? Values
    {
        get => GetValue(ValuesProperty);
        set => SetValue(ValuesProperty, value);
    }

    public IBrush Stroke
    {
        get => GetValue(StrokeProperty);
        set => SetValue(StrokeProperty, value);
    }

    public double StrokeThickness
    {
        get => GetValue(StrokeThicknessProperty);
        set => SetValue(StrokeThicknessProperty, value);
    }

    static SparklineControl()
    {
        AffectsRender<SparklineControl>(ValuesProperty, StrokeProperty, StrokeThicknessProperty);
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        var values = Values?.ToList();
        if (values is null || values.Count < 2)
        {
            return;
        }

        var width = Bounds.Width;
        var height = Bounds.Height;
        if (width <= 0 || height <= 0)
        {
            return;
        }

        var max = Math.Max(1.0, values.Max());
        var stepX = width / (values.Count - 1);

        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            for (var i = 0; i < values.Count; i++)
            {
                var x = i * stepX;
                var normalized = Math.Clamp(values[i] / max, 0, 1);
                var y = height - (normalized * height);
                var point = new Point(x, y);

                if (i == 0)
                {
                    ctx.BeginFigure(point, isFilled: false);
                }
                else
                {
                    ctx.LineTo(point);
                }
            }
            ctx.EndFigure(isClosed: false);
        }

        var pen = new Pen(Stroke, StrokeThickness, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);
        context.DrawGeometry(null, pen, geometry);
    }
}
