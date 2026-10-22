using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using RedeScan.Core.Models;
using RedeScan.UI.ViewModels;

namespace RedeScan.UI.Views;

/// <summary>
/// Modal de edição/criação de câmera. Fecha retornando a
/// <see cref="RtspCameraConfig"/> confirmada, ou <c>null</c> se cancelado.
/// </summary>
public partial class CameraEditorWindow : Window
{
    public CameraEditorWindow()
    {
        InitializeComponent();
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    private void OnConfirm(object? sender, RoutedEventArgs e)
    {
        var result = (DataContext as CameraEditorViewModel)?.BuildResult();
        Close(result);
    }

    private void OnCancel(object? sender, RoutedEventArgs e) => Close(null);
}
