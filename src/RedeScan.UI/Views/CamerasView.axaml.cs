using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using RedeScan.UI.ViewModels;

namespace RedeScan.UI.Views;

public partial class CamerasView : UserControl
{
    public CamerasView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    private CamerasViewModel? Vm => DataContext as CamerasViewModel;

    // ---- Importar/Exportar rtspList.json (formato CentralMonitor) ----
    private static readonly FilePickerFileType JsonType = new("Arquivos JSON")
    {
        Patterns = new[] { "*.json" }
    };

    private async void OnImportClick(object? sender, RoutedEventArgs e)
    {
        if (Vm is not { } vm || TopLevel.GetTopLevel(this) is not { } top)
        {
            return;
        }

        var files = await top.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Importar rtspList.json",
            AllowMultiple = false,
            FileTypeFilter = new[] { JsonType }
        });

        var path = files.Count > 0 ? files[0].TryGetLocalPath() : null;
        if (!string.IsNullOrWhiteSpace(path) && vm.ImportRtspListCommand.CanExecute(path))
        {
            vm.ImportRtspListCommand.Execute(path);
        }
    }

    private async void OnExportClick(object? sender, RoutedEventArgs e)
    {
        if (Vm is not { } vm || TopLevel.GetTopLevel(this) is not { } top)
        {
            return;
        }

        var file = await top.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Exportar rtspList.json",
            SuggestedFileName = "rtspList.json",
            DefaultExtension = "json",
            FileTypeChoices = new[] { JsonType }
        });

        var path = file?.TryGetLocalPath();
        if (!string.IsNullOrWhiteSpace(path) && vm.ExportRtspListCommand.CanExecute(path))
        {
            vm.ExportRtspListCommand.Execute(path);
        }
    }

    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        if (Vm is { } vm && vm.LoadCommand.CanExecute(null))
        {
            vm.LoadCommand.Execute(null);
        }
    }

    /// <summary>Duplo clique numa câmera da lista: reproduz no próximo slot livre.</summary>
    private void OnCameraDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (Vm is { } vm && vm.PlaySelectedCommand.CanExecute(null))
        {
            vm.PlaySelectedCommand.Execute(null);
        }
    }
}
