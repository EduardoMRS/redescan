using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using RedeScan.UI.ViewModels;

namespace RedeScan.UI.Views;

public partial class TftpView : UserControl
{
    public TftpView() => InitializeComponent();

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    private async void OnChooseFolder(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not TftpViewModel vm || TopLevel.GetTopLevel(this) is not { } top)
        {
            return;
        }

        var folders = await top.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Pasta raiz do TFTP (firmware)",
            AllowMultiple = false
        });

        var path = folders.Count > 0 ? folders[0].TryGetLocalPath() : null;
        if (!string.IsNullOrWhiteSpace(path))
        {
            vm.RootDirectory = path;
        }
    }
}
