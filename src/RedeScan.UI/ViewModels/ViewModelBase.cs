using CommunityToolkit.Mvvm.ComponentModel;

namespace RedeScan.UI.ViewModels;

/// <summary>
/// Base para todos os ViewModels da aplicação. Fornece o suporte a
/// <see cref="INotifyPropertyChanged"/> via CommunityToolkit.Mvvm e metadados
/// comuns de navegação (título e ícone exibidos na barra lateral).
/// </summary>
public abstract partial class ViewModelBase : ObservableObject
{
    /// <summary>Título legível exibido no cabeçalho e no item de menu.</summary>
    public abstract string Title { get; }

    /// <summary>Glifo/emoji simples exibido ao lado do título na navegação.</summary>
    public virtual string Icon => "•";

    /// <summary>
    /// Chave estável do módulo, usada para persistir preferências (tela inicial,
    /// módulos habilitados) sem depender do título. O padrão deriva do nome do
    /// tipo (ex.: <c>CamerasViewModel</c> → "cameras"); módulos podem sobrescrever.
    /// </summary>
    public virtual string Key =>
        GetType().Name.Replace("ViewModel", string.Empty).ToLowerInvariant();
}
