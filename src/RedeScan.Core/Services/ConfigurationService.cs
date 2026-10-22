using System.Text.Json;
using RedeScan.Core.Models;

namespace RedeScan.Core.Services;

public class ConfigurationService : IConfigurationService
{
    private readonly string _configFilePath;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public ConfigurationService(string? customFilePath = null)
    {
        if (!string.IsNullOrWhiteSpace(customFilePath))
        {
            _configFilePath = customFilePath;
        }
        else
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            if (string.IsNullOrWhiteSpace(appData))
            {
                appData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config");
            }

            var dir = Path.Combine(appData, "redescan");
            _configFilePath = Path.Combine(dir, "config.json");
        }
    }

    public string GetConfigFilePath() => _configFilePath;

    public AppSettings LoadSettings()
    {
        try
        {
            if (!File.Exists(_configFilePath))
            {
                var defaults = AppSettings.CreateDefault();
                TrySaveSync(defaults);
                return defaults;
            }

            var json = File.ReadAllText(_configFilePath);
            var settings = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions);
            return settings ?? AppSettings.CreateDefault();
        }
        catch
        {
            return AppSettings.CreateDefault();
        }
    }

    /// <summary>Grava de forma síncrona, ignorando falhas (best-effort no startup).</summary>
    private void TrySaveSync(AppSettings settings)
    {
        try
        {
            var dir = Path.GetDirectoryName(_configFilePath);
            if (!string.IsNullOrWhiteSpace(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            var json = JsonSerializer.Serialize(settings, JsonOptions);
            File.WriteAllText(_configFilePath, json);
        }
        catch
        {
            // Sem permissão de escrita, etc. — não deve impedir o app de abrir.
        }
    }

    public async Task<AppSettings> LoadSettingsAsync(CancellationToken ct = default)
    {
        try
        {
            if (!File.Exists(_configFilePath))
            {
                var defaults = AppSettings.CreateDefault();
                await SaveSettingsAsync(defaults, ct);
                return defaults;
            }

            using var stream = File.OpenRead(_configFilePath);
            var settings = await JsonSerializer.DeserializeAsync<AppSettings>(stream, JsonOptions, ct);
            return settings ?? AppSettings.CreateDefault();
        }
        catch
        {
            return AppSettings.CreateDefault();
        }
    }

    public async Task SaveSettingsAsync(AppSettings settings, CancellationToken ct = default)
    {
        var dir = Path.GetDirectoryName(_configFilePath);
        if (!string.IsNullOrWhiteSpace(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        using var stream = File.Create(_configFilePath);
        await JsonSerializer.SerializeAsync(stream, settings, JsonOptions, ct);
    }
}
