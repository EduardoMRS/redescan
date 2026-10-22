namespace RedeScan.UI.Services;

/// <summary>
/// Permite que qualquer ViewModel solicite a navegação para o navegador
/// integrado (abrindo uma URL) sem conhecer o shell. O
/// <c>MainWindowViewModel</c> implementa esta abstração.
/// </summary>
public interface IAppNavigator
{
    /// <summary>Abre a URL no módulo de navegador integrado e foca nele.</summary>
    void OpenInInternalBrowser(string url);
}
