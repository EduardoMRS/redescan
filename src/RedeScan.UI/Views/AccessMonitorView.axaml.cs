using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using RedeScan.UI.ViewModels;

namespace RedeScan.UI.Views;

public partial class AccessMonitorView : UserControl
{
    public AccessMonitorView() => InitializeComponent();

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    private async void OnExportClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not AccessMonitorViewModel vm || TopLevel.GetTopLevel(this) is not { } top)
        {
            return;
        }

        var file = await top.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Exportar histórico de acessos",
            SuggestedFileName = $"acessos-{DateTime.Now:yyyyMMdd-HHmm}.csv",
            DefaultExtension = "csv",
            FileTypeChoices = new[] { new FilePickerFileType("CSV") { Patterns = new[] { "*.csv" } } }
        });

        var path = file?.TryGetLocalPath();
        if (!string.IsNullOrWhiteSpace(path))
        {
            await vm.ExportCsvCommand.ExecuteAsync(path);
        }
    }
}
