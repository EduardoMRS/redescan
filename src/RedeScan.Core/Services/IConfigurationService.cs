using RedeScan.Core.Models;

namespace RedeScan.Core.Services;

public interface IConfigurationService
{
    Task<AppSettings> LoadSettingsAsync(CancellationToken ct = default);
    Task SaveSettingsAsync(AppSettings settings, CancellationToken ct = default);
    string GetConfigFilePath();
}
