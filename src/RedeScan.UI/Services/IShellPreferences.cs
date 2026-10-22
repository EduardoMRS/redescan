using RedeScan.Core.Models;

namespace RedeScan.UI.Services;

/// <summary>
/// Permite ao módulo de configurações conversar com o shell (janela principal)
/// sem acoplamento direto: listar os módulos, aplicar preferências (tema +
/// módulos visíveis) e trocar o tema ao vivo. Implementado pelo
/// <c>MainWindowViewModel</c>.
/// </summary>
public interface IShellPreferences
{
    /// <summary>Todos os módulos do shell: chave estável, título e ícone.</summary>
    IReadOnlyList<(string Key, string Title, string Icon)> GetModules();

    /// <summary>Aplica as preferências (tema, módulos visíveis) imediatamente.</summary>
    void ApplyPreferences(AppSettings settings);

    /// <summary>Troca o tema ao vivo ("Dark" ou "Light").</summary>
    void SetTheme(string theme);

    /// <summary>Define o módulo (por chave) como tela inicial e persiste.</summary>
    void SetStartupModule(string moduleKey);
}
