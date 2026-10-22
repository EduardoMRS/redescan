using Avalonia;
using Avalonia.Controls;
using Avalonia.Styling;
using RedeScan.UI.ViewModels;

namespace RedeScan.UI.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (DataContext is MainWindowViewModel vm)
        {
            ApplyTheme(vm.IsDarkTheme);
            vm.PropertyChanged += (_, args) =>
            {
                if (args.PropertyName == nameof(MainWindowViewModel.IsDarkTheme))
                {
                    ApplyTheme(vm.IsDarkTheme);
                }
            };
        }
    }

    private static void ApplyTheme(bool isDark)
    {
        if (Application.Current is { } app)
        {
            app.RequestedThemeVariant = isDark ? ThemeVariant.Dark : ThemeVariant.Light;
        }
    }
}
