using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RedeScan.Core.Models;
using RedeScan.Core.Services;

namespace RedeScan.UI.ViewModels;

/// <summary>Um grupo de câmeras (por <see cref="RtspCameraConfig.Group"/>) na grade.</summary>
public sealed class CameraGroup
{
    public string Name { get; }
    public ObservableCollection<RtspCameraConfig> Cameras { get; }

    public CameraGroup(string name, IEnumerable<RtspCameraConfig> cameras)
    {
        Name = name;
        Cameras = new ObservableCollection<RtspCameraConfig>(cameras);
    }
}

/// <summary>
/// Módulo 1 — gerenciamento das câmeras RTSP em grade. As câmeras são
/// persistidas no JSON de configuração via <see cref="IConfigurationService"/>.
/// As credenciais NUNCA ficam em código: são lidas/gravadas no perfil do
/// usuário e injetadas na URL apenas em tempo de execução por
/// <see cref="RtspCameraConfig.GetAuthenticatedUrl"/>.
/// </summary>
public partial class CamerasViewModel : ViewModelBase
{
    private readonly IConfigurationService _config;
    private readonly IRtspListImporter _rtspImporter;
    private AppSettings? _settings;

    public override string Title => "Câmeras RTSP";
    public override string Icon => "📷";

    public ObservableCollection<RtspCameraConfig> Cameras { get; } = new();

    /// <summary>Câmeras agrupadas por <see cref="RtspCameraConfig.Group"/> para a grade.</summary>
    public ObservableCollection<CameraGroup> Groups { get; } = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelection))]
    private RtspCameraConfig? _selectedCamera;

    [ObservableProperty]
    private string _status = "Carregando câmeras…";

    [ObservableProperty]
    private int _gridColumns = 2;

    public bool HasSelection => SelectedCamera is not null;

    /// <summary>
    /// Pedido de reprodução do stream de uma câmera. A View (quando compilada
    /// com mídia nativa) assina este evento e inicia o LibVLC; sem mídia nativa,
    /// nada acontece além da mensagem de status.
    /// </summary>
    public event Action<RtspCameraConfig>? PlayRequested;

    /// <summary>Pedido para parar a reprodução atual.</summary>
    public event Action? StopRequested;

    public CamerasViewModel(IConfigurationService config, IRtspListImporter rtspImporter)
    {
        _config = config;
        _rtspImporter = rtspImporter;
    }

    [RelayCommand]
    private async Task LoadAsync(CancellationToken token)
    {
        _settings = await _config.LoadSettingsAsync(token);
        Cameras.Clear();
        foreach (var cam in _settings.Cameras.OrderBy(c => c.DisplayOrder))
        {
            Cameras.Add(cam);
        }

        RebuildGroups();
        RecalculateGrid();
        Status = Cameras.Count == 0
            ? "Nenhuma câmera cadastrada. Clique em Adicionar ou Importar."
            : $"{Cameras.Count} câmera(s) em {Groups.Count} grupo(s).";
    }

    /// <summary>Reconstrói a visão agrupada por grupo (host) a partir de Cameras.</summary>
    private void RebuildGroups()
    {
        Groups.Clear();
        var grouped = Cameras
            .GroupBy(c => string.IsNullOrWhiteSpace(c.Group) ? "Geral" : c.Group)
            .OrderBy(g => g.Key);

        foreach (var g in grouped)
        {
            Groups.Add(new CameraGroup(g.Key, g.OrderBy(c => c.DisplayOrder)));
        }
    }

    /// <summary>
    /// Importa câmeras de um arquivo rtspList.json (formato CentralMonitor).
    /// As câmeras importadas são mescladas às existentes.
    /// </summary>
    [RelayCommand]
    private async Task ImportRtspListAsync(string? filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            Status = "Nenhum arquivo selecionado para importar.";
            return;
        }

        try
        {
            var imported = await _rtspImporter.ImportAsync(filePath);
            foreach (var cam in imported)
            {
                cam.DisplayOrder = Cameras.Count;
                Cameras.Add(cam);
            }

            RebuildGroups();
            RecalculateGrid();
            Status = $"{imported.Count} câmera(s) importada(s) de {Path.GetFileName(filePath)}. Lembre de salvar.";
        }
        catch (Exception ex)
        {
            Status = $"Falha ao importar: {ex.Message}";
        }
    }

    /// <summary>Exporta as câmeras atuais no formato rtspList.json (CentralMonitor).</summary>
    [RelayCommand]
    private async Task ExportRtspListAsync(string? filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            Status = "Nenhum destino selecionado para exportar.";
            return;
        }

        try
        {
            await _rtspImporter.ExportAsync(filePath, Cameras);
            Status = $"{Cameras.Count} câmera(s) exportada(s) para {Path.GetFileName(filePath)}.";
        }
        catch (Exception ex)
        {
            Status = $"Falha ao exportar: {ex.Message}";
        }
    }

    [RelayCommand]
    private void AddCamera()
    {
        var cam = new RtspCameraConfig
        {
            Name = $"Câmera {Cameras.Count + 1}",
            DisplayOrder = Cameras.Count
        };
        Cameras.Add(cam);
        SelectedCamera = cam;
        RebuildGroups();
        RecalculateGrid();
        Status = "Nova câmera adicionada. Preencha os dados e salve.";
    }

    [RelayCommand]
    private void RemoveCamera()
    {
        if (SelectedCamera is null)
        {
            return;
        }

        var name = SelectedCamera.Name;
        Cameras.Remove(SelectedCamera);
        SelectedCamera = null;
        RebuildGroups();
        RecalculateGrid();
        Status = $"Câmera \"{name}\" removida. Lembre de salvar.";
    }

    [RelayCommand]
    private async Task SaveAsync(CancellationToken token)
    {
        _settings ??= await _config.LoadSettingsAsync(token);

        for (var i = 0; i < Cameras.Count; i++)
        {
            Cameras[i].DisplayOrder = i;
        }

        _settings.Cameras = Cameras.ToList();
        await _config.SaveSettingsAsync(_settings, token);
        Status = $"Configuração salva em {_config.GetConfigFilePath()}";
    }

    private void RecalculateGrid()
    {
        var count = Cameras.Count(c => c.IsEnabled);
        GridColumns = count <= 1 ? 1 : (int)Math.Ceiling(Math.Sqrt(count));
        if (GridColumns < 1)
        {
            GridColumns = 1;
        }
    }

    // ------------------------------------------------------ Menu de contexto
    /// <summary>
    /// Abre o stream da câmera em um player externo (VLC, se disponível). A URL
    /// autenticada é montada por <see cref="RtspCameraConfig.GetAuthenticatedUrl"/>,
    /// mantendo as credenciais fora de qualquer log ou código.
    /// </summary>
    [RelayCommand]
    private void OpenExternal(RtspCameraConfig? camera)
    {
        camera ??= SelectedCamera;
        var url = camera?.GetAuthenticatedUrl();
        if (string.IsNullOrWhiteSpace(url))
        {
            Status = "Câmera sem URL de stream configurada.";
            return;
        }

        try
        {
            LaunchExternalPlayer(url);
            Status = $"Abrindo \"{camera!.Name}\" no player externo…";
        }
        catch (Exception ex)
        {
            Status = $"Não foi possível abrir o player: {ex.Message}";
        }
    }

    private static void LaunchExternalPlayer(string url)
    {
        // Tenta o VLC diretamente; se não existir, delega ao handler do SO.
        try
        {
            var vlc = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "vlc.exe" : "vlc";
            Process.Start(new ProcessStartInfo(vlc, $"\"{url}\"") { UseShellExecute = true });
            return;
        }
        catch
        {
            // Sem VLC no PATH — cai no fallback do sistema.
        }

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

    [RelayCommand]
    private void EditCamera(RtspCameraConfig? camera)
    {
        if (camera is not null)
        {
            SelectedCamera = camera;
        }
    }

    [RelayCommand]
    private void PlaySelected()
    {
        if (SelectedCamera is null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(SelectedCamera.Url))
        {
            Status = "Câmera sem URL de stream configurada.";
            return;
        }

        PlayRequested?.Invoke(SelectedCamera);
        Status = $"Reproduzindo \"{SelectedCamera.Name}\"…";
    }

    [RelayCommand]
    private void StopSelected()
    {
        StopRequested?.Invoke();
        Status = "Reprodução parada.";
    }
}
