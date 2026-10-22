using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RedeScan.Core.Models;
using RedeScan.Core.Services;

namespace RedeScan.UI.ViewModels;

/// <summary>
/// Módulo 5 — atalhos de rede (roteadores, ONUs e ferramentas web). Abre os
/// alvos no navegador padrão do sistema de forma multiplataforma. A integração
/// do navegador embutido (Avalonia.WebView) pode ser adicionada depois sem
/// alterar este modelo de dados.
/// </summary>
public partial class ShortcutsViewModel : ViewModelBase
{
    private readonly IConfigurationService _config;
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

    public ShortcutsViewModel(IConfigurationService config)
    {
        _config = config;
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

    [RelayCommand]
    private void Open(NetworkShortcut? shortcut)
    {
        shortcut ??= SelectedShortcut;
        if (shortcut is null || string.IsNullOrWhiteSpace(shortcut.Target))
        {
            return;
        }

        try
        {
            OpenInSystemBrowser(shortcut.Target);
            Status = $"Abrindo {shortcut.Target}…";
        }
        catch (Exception ex)
        {
            Status = $"Não foi possível abrir: {ex.Message}";
        }
    }

    private static void OpenInSystemBrowser(string target)
    {
        var url = target.Contains("://") ? target : $"http://{target}";

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            Process.Start("xdg-open", url);
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            Process.Start("open", url);
        }
    }
}
