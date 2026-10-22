using RedeScan.Core.Models;

namespace RedeScan.Core.Services;

public interface IConfigurationService
{
    Task<AppSettings> LoadSettingsAsync(CancellationToken ct = default);
    Task SaveSettingsAsync(AppSettings settings, CancellationToken ct = default);

    /// <summary>
    /// Carrega as configurações de forma totalmente síncrona (sem I/O async).
    /// Seguro para chamar na thread de UI durante o startup, onde bloquear uma
    /// chamada assíncrona com <c>GetAwaiter().GetResult()</c> pode travar.
    /// </summary>
    AppSettings LoadSettings();

    string GetConfigFilePath();
}
