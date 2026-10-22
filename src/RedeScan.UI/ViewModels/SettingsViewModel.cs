using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RedeScan.Core.Models;
using RedeScan.Core.Services;
using RedeScan.UI.Services;

namespace RedeScan.UI.ViewModels;

/// <summary>Representa um módulo no painel de configurações: pode ser habilitado
/// (visível no menu) e escolhido como tela inicial.</summary>
public partial class ModuleToggle : ObservableObject
{
    public string Key { get; }
    public string Title { get; }
    public string Icon { get; }

    [ObservableProperty]
    private bool _isEnabled = true;

    public string Display => $"{Icon}  {Title}";

    public ModuleToggle(string key, string title, string icon)
    {
        Key = key;
        Title = title;
        Icon = icon;
    }
}

/// <summary>
/// Módulo de configurações: define a tela inicial, quais módulos ficam visíveis,
/// o range padrão do scan de portas, a página inicial do navegador embutido e o
/// tema. As mudanças são aplicadas ao shell e persistidas em disco.
/// </summary>
public partial class SettingsViewModel : ViewModelBase
{
    private readonly IConfigurationService _config;
    private readonly IServiceProvider _services;
    private IShellPreferences? _shellRef;
    private AppSettings? _settings;
    private bool _loading;

    /// <summary>Resolve o shell sob demanda, evitando ciclo de construção no DI.</summary>
    private IShellPreferences Shell =>
        _shellRef ??= (IShellPreferences)_services.GetService(typeof(IShellPreferences))!;

    public override string Title => "Configurações";
    public override string Icon => "⚙️";
    public override string Key => "settings";

    /// <summary>Módulos disponíveis (habilitar/ocultar + escolher como inicial).</summary>
    public ObservableCollection<ModuleToggle> Modules { get; } = new();

    public string[] Themes { get; } = { "Dark", "Light" };

    [ObservableProperty] private ModuleToggle? _startupModule;
    [ObservableProperty] private string _theme = "Dark";
    [ObservableProperty] private string _browserHome = "router";
    [ObservableProperty] private int _portScanStart = 1;
    [ObservableProperty] private int _portScanEnd = 1024;
    [ObservableProperty] private string _status = "Carregando preferências…";

    public SettingsViewModel(IConfigurationService config, IServiceProvider services)
    {
        _config = config;
        _services = services;
    }

    [RelayCommand]
    private async Task LoadAsync(CancellationToken token)
    {
        _loading = true;
        _settings = await _config.LoadSettingsAsync(token);

        Modules.Clear();
        var enabled = _settings.EnabledModules;
        foreach (var (key, title, icon) in Shell.GetModules())
        {
            // "settings" não pode ser desabilitado — sempre acessível.
            var toggle = new ModuleToggle(key, title, icon)
            {
                IsEnabled = key == "settings" || enabled.Count == 0 || enabled.Contains(key)
            };
            Modules.Add(toggle);
        }

        var startupKey = string.IsNullOrWhiteSpace(_settings.StartupPageKey)
            ? "dashboard"
            : _settings.StartupPageKey;
        StartupModule = Modules.FirstOrDefault(m => m.Key == startupKey) ?? Modules.FirstOrDefault();

        Theme = string.IsNullOrWhiteSpace(_settings.Theme) ? "Dark" : _settings.Theme;
        BrowserHome = string.IsNullOrWhiteSpace(_settings.BrowserHomeUrl) ? "router" : _settings.BrowserHomeUrl;
        PortScanStart = _settings.PortScanStart;
        PortScanEnd = _settings.PortScanEnd;

        _loading = false;
        Status = "Preferências carregadas. As mudanças aplicam ao salvar.";
    }

    [RelayCommand]
    private async Task SaveAsync(CancellationToken token)
    {
        _settings ??= await _config.LoadSettingsAsync(token);

        if (PortScanStart < 1) PortScanStart = 1;
        if (PortScanEnd > 65535) PortScanEnd = 65535;
        if (PortScanEnd < PortScanStart) PortScanEnd = PortScanStart;

        _settings.StartupPageKey = StartupModule?.Key ?? "dashboard";
        _settings.Theme = Theme;
        _settings.BrowserHomeUrl = string.IsNullOrWhiteSpace(BrowserHome) ? "router" : BrowserHome.Trim();
        _settings.PortScanStart = PortScanStart;
        _settings.PortScanEnd = PortScanEnd;
        _settings.EnabledModules = Modules
            .Where(m => m.IsEnabled)
            .Select(m => m.Key)
            .ToList();

        await _config.SaveSettingsAsync(_settings, token);

        // Aplica ao shell imediatamente (tema + módulos visíveis).
        Shell.ApplyPreferences(_settings);

        Status = $"Preferências salvas em {_config.GetConfigFilePath()}";
    }

    partial void OnThemeChanged(string value)
    {
        if (!_loading)
        {
            Shell.SetTheme(value);
        }
    }
}
