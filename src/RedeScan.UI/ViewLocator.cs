using Avalonia.Controls;
using Avalonia.Controls.Templates;
using RedeScan.UI.ViewModels;

namespace RedeScan.UI;

/// <summary>
/// Mapeia convencionalmente um ViewModel para sua View correspondente:
/// <c>RedeScan.UI.ViewModels.XxxViewModel</c> → <c>RedeScan.UI.Views.XxxView</c>.
/// </summary>
public class ViewLocator : IDataTemplate
{
    public Control Build(object? data)
    {
        if (data is null)
        {
            return new TextBlock { Text = "null" };
        }

        var name = data.GetType().FullName!
            .Replace("ViewModels", "Views", StringComparison.Ordinal)
            .Replace("ViewModel", "View", StringComparison.Ordinal);

        var type = Type.GetType(name);
        if (type is not null)
        {
            return (Control)Activator.CreateInstance(type)!;
        }

        return new TextBlock { Text = "Não encontrado: " + name };
    }

    public bool Match(object? data) => data is ViewModelBase;
}
