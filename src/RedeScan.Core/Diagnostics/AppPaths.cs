namespace RedeScan.Core.Diagnostics;

/// <summary>
/// Pastas usadas pelo app em runtime. Prefere a pasta do próprio executável
/// (instalação portátil/pasta gravável) e cai para o perfil do usuário quando
/// ela não é gravável (ex.: /opt/redescan instalado via .deb sem sudo).
/// </summary>
public static class AppPaths
{
    private static readonly Lazy<string> _userDataDir = new(() =>
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        if (string.IsNullOrWhiteSpace(appData))
        {
            appData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config");
        }

        return Path.Combine(appData, "redescan");
    });

    /// <summary>Pasta de dados do usuário (~/.config/redescan ou %APPDATA%\redescan).</summary>
    public static string UserDataDir => _userDataDir.Value;

    /// <summary>Pasta onde o executável está instalado.</summary>
    public static string AppDir => AppContext.BaseDirectory;

    /// <summary>
    /// Pasta de logs: <c>{pasta do app}/logs</c> quando gravável; senão
    /// <c>{dados do usuário}/logs</c>.
    /// </summary>
    public static string LogsDir => _logsDir.Value;

    private static readonly Lazy<string> _logsDir = new(() =>
        FirstWritable(Path.Combine(AppDir, "logs"), Path.Combine(UserDataDir, "logs")));

    /// <summary>
    /// Pasta de ferramentas externas baixadas pelo app (ffmpeg, WinBox). Fica no
    /// perfil do usuário para funcionar sem privilégio de administrador.
    /// </summary>
    public static string ToolsDir => Path.Combine(UserDataDir, "tools");

    private static string FirstWritable(params string[] candidates)
    {
        foreach (var dir in candidates)
        {
            try
            {
                Directory.CreateDirectory(dir);
                // Nome único: a pasta pode ser compartilhada entre usuários.
                var probe = Path.Combine(dir, $".write-test-{Environment.ProcessId}-{Guid.NewGuid():N}");
                File.WriteAllText(probe, string.Empty);
                File.Delete(probe);
                return dir;
            }
            catch
            {
                // sem permissão — tenta o próximo
            }
        }

        return Path.GetTempPath();
    }
}
