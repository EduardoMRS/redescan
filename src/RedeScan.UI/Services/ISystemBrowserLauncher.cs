namespace RedeScan.UI.Services;

/// <summary>Abre uma URL no navegador padrão do sistema operacional.</summary>
public interface ISystemBrowserLauncher
{
    void Open(string target);
}
