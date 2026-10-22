using System.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RedeScan.Core.Models;
using RedeScan.Core.Services;
using RedeScan.UI.Services;

namespace RedeScan.UI.ViewModels;

/// <summary>
/// Módulo de atalhos de rede (roteadores, ONUs e ferramentas web). Abre os
/// alvos tanto no navegador integrado quanto no navegador padrão do sistema.
/// </summary>
public partial class ShortcutsViewModel : ViewModelBase
{
    private readonly IConfigurationService _config;
    private readonly ISystemBrowserLauncher _systemBrowser;
    private readonly IServiceProvider _services;
    private AppSettings? _settings;

    public override string Title => "Atalhos";
    public override string Icon => "🔗";

    public ObservableCollection<NetworkShortcut> Shortcuts { get; } = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelection))]
    private NetworkShortcut? _selectedShortcut;

    [ObservableProperty]
    private string _status = "Carregando atalhos…";

    public bool HasSelection => SelectedShortcut is not null;

    public ShortcutsViewModel(
        IConfigurationService config,
        ISystemBrowserLauncher systemBrowser,
        IServiceProvider services)
    {
        _config = config;
        _systemBrowser = systemBrowser;
        _services = services;
    }

    [RelayCommand]
    private async Task LoadAsync(CancellationToken token)
    {
        _settings = await _config.LoadSettingsAsync(token);
        Shortcuts.Clear();
        foreach (var shortcut in _settings.Shortcuts)
        {
            Shortcuts.Add(shortcut);
        }

        Status = $"{Shortcuts.Count} atalho(s) disponível(is).";
    }

    [RelayCommand]
    private void AddShortcut()
    {
        var shortcut = new NetworkShortcut { Name = "Novo atalho", Target = "http://" };
        Shortcuts.Add(shortcut);
        SelectedShortcut = shortcut;
        Status = "Atalho adicionado. Preencha e salve.";
    }

    [RelayCommand]
    private void RemoveShortcut()
    {
        if (SelectedShortcut is null)
        {
            return;
        }

        Shortcuts.Remove(SelectedShortcut);
        SelectedShortcut = null;
        Status = "Atalho removido. Lembre de salvar.";
    }

    [RelayCommand]
    private async Task SaveAsync(CancellationToken token)
    {
        _settings ??= await _config.LoadSettingsAsync(token);
        _settings.Shortcuts = Shortcuts.ToList();
        await _config.SaveSettingsAsync(_settings, token);
        Status = "Atalhos salvos.";
    }

    /// <summary>Abre o atalho no navegador integrado da aplicação.</summary>
    [RelayCommand]
    private void Open(NetworkShortcut? shortcut)
    {
        shortcut ??= SelectedShortcut;
        if (shortcut is null || string.IsNullOrWhiteSpace(shortcut.Target))
        {
            return;
        }

        var navigator = _services.GetService(typeof(IAppNavigator)) as IAppNavigator;
        navigator?.OpenInInternalBrowser(shortcut.Target);
        Status = $"Abrindo {shortcut.Target} no navegador integrado…";
    }

    /// <summary>Abre o atalho no navegador padrão do sistema.</summary>
    [RelayCommand]
    private void OpenInSystem(NetworkShortcut? shortcut)
    {
        shortcut ??= SelectedShortcut;
        if (shortcut is null || string.IsNullOrWhiteSpace(shortcut.Target))
        {
            return;
        }

        try
        {
            _systemBrowser.Open(shortcut.Target);
            Status = $"Abrindo {shortcut.Target} no navegador do sistema…";
        }
        catch (Exception ex)
        {
            Status = $"Não foi possível abrir: {ex.Message}";
        }
    }
}
