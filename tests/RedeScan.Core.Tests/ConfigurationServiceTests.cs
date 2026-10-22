using RedeScan.Core.Models;
using RedeScan.Core.Services;

namespace RedeScan.Core.Tests;

public class ConfigurationServiceTests : IDisposable
{
    private readonly string _tempFile;

    public ConfigurationServiceTests()
    {
        _tempFile = Path.Combine(Path.GetTempPath(), $"redescan_test_{Guid.NewGuid():N}.json");
    }

    public void Dispose()
    {
        if (File.Exists(_tempFile))
        {
            try { File.Delete(_tempFile); } catch { }
        }
    }

    [Fact]
    public async Task LoadSettings_WhenFileDoesNotExist_CreatesDefaults()
    {
        var service = new ConfigurationService(_tempFile);

        var settings = await service.LoadSettingsAsync();

        Assert.NotNull(settings);
        Assert.NotEmpty(settings.Shortcuts);
        Assert.Contains(settings.Shortcuts, s => s.Target == "192.168.0.1");
        Assert.True(File.Exists(_tempFile));
    }

    [Fact]
    public async Task SaveSettings_PersistsCamerasAndShortcuts()
    {
        var service = new ConfigurationService(_tempFile);
        var settings = new AppSettings
        {
            Cameras = new List<RtspCameraConfig>
            {
                new()
                {
                    Name = "Portão Principal",
                    StreamUrl = "rtsp://192.168.1.100:554/live",
                    Username = "admin",
                    Password = "segredo123"
                }
            },
            Shortcuts = new List<NetworkShortcut>
            {
                new() { Name = "Roteador Novo", Target = "192.168.15.1" }
            }
        };

        await service.SaveSettingsAsync(settings);
        var reloaded = await service.LoadSettingsAsync();

        Assert.Single(reloaded.Cameras);
        Assert.Equal("Portão Principal", reloaded.Cameras[0].Name);
        Assert.Equal("admin", reloaded.Cameras[0].Username);
        Assert.Equal("segredo123", reloaded.Cameras[0].Password);

        var authUrl = reloaded.Cameras[0].GetAuthenticatedUrl();
        Assert.Equal("rtsp://admin:segredo123@192.168.1.100:554/live", authUrl);
    }

    [Fact]
    public void LoadSettingsSync_WhenFileDoesNotExist_CreatesDefaultsWithoutBlocking()
    {
        // Caminho crítico de startup: o shell carrega as prefs de forma síncrona
        // na UI thread. Precisa criar os defaults e NUNCA travar/lançar.
        var service = new ConfigurationService(_tempFile);

        var settings = service.LoadSettings();

        Assert.NotNull(settings);
        Assert.NotEmpty(settings.Shortcuts);
        Assert.Equal("dashboard", settings.StartupPageKey);
        Assert.True(File.Exists(_tempFile));
    }

    [Fact]
    public async Task LoadSettingsSync_ReadsBackWhatWasSaved()
    {
        var service = new ConfigurationService(_tempFile);
        var settings = new AppSettings
        {
            StartupPageKey = "cameras",
            Theme = "Light",
            BrowserHomeUrl = "https://example.test",
            PortScanStart = 20,
            PortScanEnd = 443,
            EnabledModules = new List<string> { "dashboard", "cameras" }
        };

        await service.SaveSettingsAsync(settings);
        var reloaded = service.LoadSettings();

        Assert.Equal("cameras", reloaded.StartupPageKey);
        Assert.Equal("Light", reloaded.Theme);
        Assert.Equal("https://example.test", reloaded.BrowserHomeUrl);
        Assert.Equal(20, reloaded.PortScanStart);
        Assert.Equal(443, reloaded.PortScanEnd);
        Assert.Equal(2, reloaded.EnabledModules.Count);
    }
}
