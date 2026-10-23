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

    /// <summary>Abre o terminal SSH embutido já apontando para o host.</summary>
    void OpenTerminal(string host, int port = 22, string? username = null);

    /// <summary>Abre a aba Telnet do terminal embutido já apontando para o host.</summary>
    void OpenTelnetTerminal(string host, int port = 23);
}
