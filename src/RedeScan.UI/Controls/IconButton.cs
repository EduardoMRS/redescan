using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Material.Icons;
using Material.Icons.Avalonia;

namespace RedeScan.UI.Controls;

/// <summary>
/// Botão com ícone vetorial (Material Design Icons) + texto. Mantém o estilo
/// padrão de <see cref="Button"/> (classes accent/ghost/danger etc. funcionam).
/// Sem <see cref="Text"/>, vira um botão só de ícone (use ToolTip.Tip).
/// </summary>
public class IconButton : Button
{
    public static readonly StyledProperty<MaterialIconKind?> IconProperty =
        AvaloniaProperty.Register<IconButton, MaterialIconKind?>(nameof(Icon));

    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<IconButton, string?>(nameof(Text));

    private readonly MaterialIcon _icon = new() { Width = 16, Height = 16, VerticalAlignment = VerticalAlignment.Center };
    private readonly TextBlock _text = new() { VerticalAlignment = VerticalAlignment.Center };

    public IconButton()
    {
        Content = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 6,
            Children = { _icon, _text }
        };
        Sync();
    }

    protected override Type StyleKeyOverride => typeof(Button);

    public MaterialIconKind? Icon
    {
        get => GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    public string? Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IconProperty || change.Property == TextProperty)
        {
            Sync();
        }
    }

    private void Sync()
    {
        _icon.IsVisible = Icon is not null;
        if (Icon is { } kind)
        {
            _icon.Kind = kind;
        }

        _text.Text = Text;
        _text.IsVisible = !string.IsNullOrEmpty(Text);
    }
}
