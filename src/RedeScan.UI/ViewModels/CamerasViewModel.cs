using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RedeScan.Core.Diagnostics;
using RedeScan.Core.Models;
using RedeScan.Core.Services;
using RedeScan.Core.Services.Media;
using RedeScan.UI.Services;

namespace RedeScan.UI.ViewModels;

/// <summary>Item da lista de câmeras: a configuração + a miniatura de prévia.</summary>
public partial class CameraListItem : ObservableObject
{
    public CameraListItem(RtspCameraConfig camera) => Camera = camera;

    public RtspCameraConfig Camera { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasThumbnail))]
    private Bitmap? _thumbnail;

    [ObservableProperty] private bool _isLoadingThumbnail;
    [ObservableProperty] private string? _thumbnailError;

    public bool HasThumbnail => Thumbnail is not null;

    public string Subtitle =>
        string.IsNullOrWhiteSpace(Camera.Location) ? Camera.Group : $"{Camera.Group} · {Camera.Location}";

    /// <summary>Notifica a UI depois que a câmera foi editada no modal.</summary>
    public void Refresh()
    {
        OnPropertyChanged(nameof(Camera));
        OnPropertyChanged(nameof(Subtitle));
    }
}

/// <summary>
/// Um slot da grade de vídeo. Quando tem câmera, expõe a URL autenticada em
/// <see cref="StreamUrl"/>; o controle de vídeo da View reproduz o que estiver
/// ali e para sozinho quando ela volta a ser nula.
/// </summary>
public partial class CameraSlot : ObservableObject
{
    public int Index { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasCamera))]
    [NotifyPropertyChangedFor(nameof(Label))]
    [NotifyPropertyChangedFor(nameof(StreamUrl))]
    private RtspCameraConfig? _camera;

    public CameraSlot(int index) => Index = index;

    public bool HasCamera => Camera is not null;
    public string Label => Camera?.Name ?? $"Slot {Index + 1} — vazio\nselecione uma câmera e clique em ▶";

    /// <summary>URL efetiva (com credenciais injetadas) ou nulo quando o slot está vazio.</summary>
    public string? StreamUrl => Camera?.GetAuthenticatedUrl() is { Length: > 0 } url ? url : null;
}

/// <summary>
/// Módulo de câmeras RTSP. Esquerda: lista com busca por nome, filtro por grupo
/// e miniatura real de cada câmera. Direita: grade de vídeo ao vivo de 1 a 6
/// câmeras (decodificada pelo ffmpeg). O editor/criador abre em um modal.
/// As credenciais NUNCA ficam em código — vêm do perfil do usuário e são
/// injetadas na URL em runtime por <see cref="RtspCameraConfig.GetAuthenticatedUrl"/>.
/// </summary>
public partial class CamerasViewModel : ViewModelBase
{
    private readonly IConfigurationService _config;
    private readonly IRtspListImporter _rtspImporter;
    private readonly ICameraEditorDialog _editorDialog;
    private readonly IVideoService _video;
    private readonly IExternalToolService _tools;
    private AppSettings? _settings;
    private CancellationTokenSource? _thumbCts;
    private bool _loaded;

    public override string Title => "Câmeras RTSP";
    public override string Icon => "📷";

    /// <summary>Todas as câmeras cadastradas (fonte de verdade).</summary>
    public ObservableCollection<RtspCameraConfig> Cameras { get; } = new();

    /// <summary>Itens visíveis na lista após busca e filtro de grupo.</summary>
    public ObservableCollection<CameraListItem> FilteredCameras { get; } = new();

    /// <summary>Grupos disponíveis para o filtro (inclui "Todos os grupos").</summary>
    public ObservableCollection<string> AvailableGroups { get; } = new();

    /// <summary>Slots de vídeo ativos (1 a 6) exibidos na grade à direita.</summary>
    public ObservableCollection<CameraSlot> ActiveSlots { get; } = new();

    /// <summary>Opções de layout da grade de vídeo.</summary>
    public int[] LayoutOptions { get; } = { 1, 2, 3, 4, 5, 6 };

    /// <summary>Miniaturas já capturadas, por Id da câmera (sobrevivem ao filtro).</summary>
    private readonly Dictionary<string, Bitmap> _thumbnails = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectedCamera))]
    [NotifyPropertyChangedFor(nameof(HasSelection))]
    private CameraListItem? _selectedItem;

    [ObservableProperty] private string _status = "Carregando câmeras…";
    [ObservableProperty] private string _searchText = string.Empty;
    [ObservableProperty] private string _groupFilter = AllGroups;
    [ObservableProperty] private int _videoLayout = 1;
    [ObservableProperty] private int _gridColumns = 1;

    /// <summary>True se o ffmpeg (decodificador) está disponível.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsFfmpegMissing))]
    private bool _isFfmpegAvailable;

    [ObservableProperty] private bool _isInstallingFfmpeg;
    [ObservableProperty] private string _installProgress = string.Empty;

    public bool IsFfmpegMissing => !IsFfmpegAvailable;

    public RtspCameraConfig? SelectedCamera => SelectedItem?.Camera;

    public bool HasSelection => SelectedItem is not null;

    public const string AllGroups = "Todos os grupos";

    public CamerasViewModel(
        IConfigurationService config,
        IRtspListImporter rtspImporter,
        ICameraEditorDialog editorDialog,
        IVideoService video,
        IExternalToolService tools)
    {
        _config = config;
        _rtspImporter = rtspImporter;
        _editorDialog = editorDialog;
        _video = video;
        _tools = tools;
        IsFfmpegAvailable = _video.IsAvailable;
        RebuildSlots();
    }

    partial void OnSearchTextChanged(string value) => ApplyFilter();
    partial void OnGroupFilterChanged(string value) => ApplyFilter();
    partial void OnVideoLayoutChanged(int value) => RebuildSlots();

    [RelayCommand]
    private async Task LoadAsync(CancellationToken token)
    {
        IsFfmpegAvailable = _video.IsAvailable;
        if (_loaded)
        {
            return; // a View recarrega ao voltar para a tela; mantém o estado
        }

        _settings = await _config.LoadSettingsAsync(token);
        Cameras.Clear();
        foreach (var cam in _settings.Cameras.OrderBy(c => c.DisplayOrder))
        {
            Cameras.Add(cam);
        }

        _loaded = true;
        RebuildGroups();
        ApplyFilter();
        Status = Cameras.Count == 0
            ? "Nenhuma câmera cadastrada. Clique em Adicionar ou Importar."
            : $"{Cameras.Count} câmera(s) em {Math.Max(0, AvailableGroups.Count - 1)} grupo(s).";

        if (IsFfmpegAvailable)
        {
            _ = RefreshThumbnailsAsync();
        }
        else
        {
            Status += " Instale o ffmpeg para ver os vídeos e as prévias.";
        }
    }

    /// <summary>Recalcula a lista de grupos disponíveis para o filtro.</summary>
    private void RebuildGroups()
    {
        var current = GroupFilter;
        AvailableGroups.Clear();
        AvailableGroups.Add(AllGroups);
        foreach (var g in Cameras
                     .Select(c => string.IsNullOrWhiteSpace(c.Group) ? "Geral" : c.Group)
                     .Distinct()
                     .OrderBy(g => g))
        {
            AvailableGroups.Add(g);
        }

        GroupFilter = AvailableGroups.Contains(current) ? current : AllGroups;
    }

    /// <summary>Aplica busca por nome/local + filtro por grupo à lista da esquerda.</summary>
    private void ApplyFilter()
    {
        var term = SearchText?.Trim() ?? string.Empty;
        var group = GroupFilter;
        var selectedId = SelectedItem?.Camera.Id;

        IEnumerable<RtspCameraConfig> query = Cameras;

        if (!string.IsNullOrEmpty(group) && group != AllGroups)
        {
            query = query.Where(c => (string.IsNullOrWhiteSpace(c.Group) ? "Geral" : c.Group) == group);
        }

        if (!string.IsNullOrEmpty(term))
        {
            query = query.Where(c =>
                (c.Name?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (c.Location?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false));
        }

        FilteredCameras.Clear();
        foreach (var cam in query.OrderBy(c => c.DisplayOrder))
        {
            var item = new CameraListItem(cam);
            if (_thumbnails.TryGetValue(cam.Id, out var thumb))
            {
                item.Thumbnail = thumb;
            }

            FilteredCameras.Add(item);
        }

        SelectedItem = FilteredCameras.FirstOrDefault(i => i.Camera.Id == selectedId);
    }

    /// <summary>Monta a coleção de slots de acordo com o layout escolhido (1–6).</summary>
    private void RebuildSlots()
    {
        var count = Math.Clamp(VideoLayout, 1, 6);
        var existing = ActiveSlots.Select(s => s.Camera).ToList();

        ActiveSlots.Clear();
        for (var i = 0; i < count; i++)
        {
            ActiveSlots.Add(new CameraSlot(i) { Camera = i < existing.Count ? existing[i] : null });
        }

        GridColumns = count switch
        {
            1 => 1,
            2 => 2,
            3 => 3,
            4 => 2,
            _ => 3
        };
    }

    // ------------------------------------------------------------ Reprodução
    /// <summary>Coloca a câmera selecionada no primeiro slot livre (ou no 1º) e reproduz.</summary>
    [RelayCommand]
    private void PlaySelected() => Play(SelectedItem);

    /// <summary>Reproduz uma câmera da lista (duplo clique / menu de contexto).</summary>
    [RelayCommand]
    private void Play(CameraListItem? item)
    {
        var camera = item?.Camera;
        if (camera is null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(camera.Url))
        {
            Status = $"\"{camera.Name}\" não tem URL de stream configurada.";
            return;
        }

        if (!IsFfmpegAvailable)
        {
            Status = "O ffmpeg é necessário para reproduzir. Clique em \"Instalar ffmpeg\".";
            return;
        }

        // Já está tocando em algum slot? Não duplica.
        var playing = ActiveSlots.FirstOrDefault(s => s.Camera?.Id == camera.Id);
        if (playing is not null)
        {
            Status = $"\"{camera.Name}\" já está no slot {playing.Index + 1}.";
            return;
        }

        var slot = ActiveSlots.FirstOrDefault(s => !s.HasCamera) ?? ActiveSlots[0];
        slot.Camera = camera;
        Status = $"Reproduzindo \"{camera.Name}\" no slot {slot.Index + 1}.";
    }

    /// <summary>Reproduz todas as câmeras visíveis até preencher os slots.</summary>
    [RelayCommand]
    private void PlayAllVisible()
    {
        foreach (var slot in ActiveSlots)
        {
            slot.Camera = null;
        }

        var cams = FilteredCameras
            .Select(i => i.Camera)
            .Where(c => c.IsEnabled && !string.IsNullOrWhiteSpace(c.Url))
            .Take(ActiveSlots.Count)
            .ToList();
        for (var i = 0; i < cams.Count; i++)
        {
            ActiveSlots[i].Camera = cams[i];
        }

        Status = cams.Count == 0 ? "Nenhuma câmera habilitada com URL na lista." : $"{cams.Count} câmera(s) na grade.";
    }

    [RelayCommand]
    private void StopSlot(CameraSlot? slot)
    {
        if (slot is null)
        {
            return;
        }

        slot.Camera = null;
        Status = $"Slot {slot.Index + 1} parado.";
    }

    [RelayCommand]
    private void StopAll()
    {
        foreach (var s in ActiveSlots)
        {
            s.Camera = null;
        }

        Status = "Todos os streams foram parados.";
    }

    // --------------------------------------------------------- ffmpeg / prévias
    [RelayCommand]
    private async Task InstallFfmpegAsync(CancellationToken ct)
    {
        if (IsInstallingFfmpeg)
        {
            return;
        }

        IsInstallingFfmpeg = true;
        InstallProgress = "Preparando download do ffmpeg…";
        var progress = new Progress<ToolDownloadProgress>(p => InstallProgress = p.Message);
        try
        {
            var path = await _tools.EnsureAsync(ExternalTool.Ffmpeg, progress, ct);
            IsFfmpegAvailable = _video.IsAvailable;
            InstallProgress = string.Empty;
            Status = $"ffmpeg pronto ({path}). Agora é só reproduzir.";
            _ = RefreshThumbnailsAsync();
        }
        catch (OperationCanceledException)
        {
            InstallProgress = string.Empty;
            Status = "Instalação do ffmpeg cancelada.";
        }
        catch (Exception ex)
        {
            AppLog.Error("Falha ao instalar o ffmpeg", ex);
            InstallProgress = string.Empty;
            Status = $"Não foi possível instalar o ffmpeg: {ex.Message}";
        }
        finally
        {
            IsInstallingFfmpeg = false;
        }
    }

    /// <summary>Captura uma miniatura real de cada câmera visível (2 por vez).</summary>
    [RelayCommand]
    private async Task RefreshThumbnailsAsync()
    {
        if (!IsFfmpegAvailable)
        {
            return;
        }

        _thumbCts?.Cancel();
        _thumbCts = new CancellationTokenSource();
        var ct = _thumbCts.Token;

        var items = FilteredCameras.Where(i => !string.IsNullOrWhiteSpace(i.Camera.Url)).ToList();
        using var gate = new SemaphoreSlim(2);
        var tasks = items.Select(async item =>
        {
            await gate.WaitAsync(ct);
            try
            {
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    item.IsLoadingThumbnail = true;
                    item.ThumbnailError = null;
                });

                var jpeg = await _video.CaptureSnapshotAsync(item.Camera.GetAuthenticatedUrl(), 240, TimeSpan.FromSeconds(10), ct);
                var error = _video.LastSnapshotError;
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    item.IsLoadingThumbnail = false;
                    if (jpeg is null)
                    {
                        item.ThumbnailError = error ?? "Sem prévia";
                        return;
                    }

                    using var ms = new MemoryStream(jpeg);
                    var bmp = new Bitmap(ms);
                    _thumbnails[item.Camera.Id] = bmp;
                    item.Thumbnail = bmp;
                });
            }
            catch (OperationCanceledException)
            {
                // nova atualização substituiu esta
            }
            catch (Exception ex)
            {
                AppLog.Warn($"Prévia de {item.Camera.Name}: {ex.Message}");
            }
            finally
            {
                gate.Release();
            }
        }).ToList();

        try
        {
            await Task.WhenAll(tasks);
        }
        catch (OperationCanceledException)
        {
            // cancelado
        }
    }

    // ----------------------------------------------- Importar / Exportar
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
            ApplyFilter();
            Status = $"{imported.Count} câmera(s) importada(s) de {Path.GetFileName(filePath)}. Lembre de salvar.";
            _ = RefreshThumbnailsAsync();
        }
        catch (Exception ex)
        {
            Status = $"Falha ao importar: {ex.Message}";
        }
    }

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

    // --------------------------------------------- Criar / Editar (modal)
    [RelayCommand]
    private async Task AddCameraAsync()
    {
        var draft = new RtspCameraConfig
        {
            Name = $"Câmera {Cameras.Count + 1}",
            DisplayOrder = Cameras.Count
        };

        var result = await _editorDialog.EditAsync(draft, isNew: true);
        if (result is null)
        {
            Status = "Criação de câmera cancelada.";
            return;
        }

        Cameras.Add(result);
        RebuildGroups();
        ApplyFilter();
        SelectedItem = FilteredCameras.FirstOrDefault(i => i.Camera.Id == result.Id);
        await SaveAsync(CancellationToken.None);
        Status = $"Câmera \"{result.Name}\" criada e salva.";
        _ = RefreshThumbnailsAsync();
    }

    [RelayCommand]
    private async Task EditCameraAsync(CameraListItem? item)
    {
        var camera = (item ?? SelectedItem)?.Camera;
        if (camera is null)
        {
            return;
        }

        var result = await _editorDialog.EditAsync(camera, isNew: false);
        if (result is null)
        {
            Status = "Edição cancelada.";
            return;
        }

        _thumbnails.Remove(result.Id);
        RebuildGroups();
        ApplyFilter();

        // Se a câmera estava tocando, reinicia o slot com a URL nova.
        foreach (var slot in ActiveSlots.Where(s => s.Camera?.Id == result.Id))
        {
            slot.Camera = null;
            slot.Camera = result;
        }

        await SaveAsync(CancellationToken.None);
        Status = $"Câmera \"{result.Name}\" atualizada e salva.";
        _ = RefreshThumbnailsAsync();
    }

    [RelayCommand]
    private async Task RemoveCameraAsync(CameraListItem? item)
    {
        var camera = (item ?? SelectedItem)?.Camera;
        if (camera is null)
        {
            return;
        }

        foreach (var slot in ActiveSlots.Where(s => s.Camera?.Id == camera.Id))
        {
            slot.Camera = null;
        }

        Cameras.Remove(camera);
        _thumbnails.Remove(camera.Id);
        RebuildGroups();
        ApplyFilter();
        await SaveAsync(CancellationToken.None);
        Status = $"Câmera \"{camera.Name}\" removida.";
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

    // ------------------------------------------------------ Player externo
    /// <summary>
    /// Abre o stream num player externo (VLC, se instalado). A URL autenticada
    /// é montada por <see cref="RtspCameraConfig.GetAuthenticatedUrl"/>.
    /// </summary>
    [RelayCommand]
    private void OpenExternal(CameraListItem? item)
    {
        var camera = (item ?? SelectedItem)?.Camera;
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
        try
        {
            var vlc = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "vlc.exe" : "vlc";
            var psi = new ProcessStartInfo(vlc) { UseShellExecute = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) };
            psi.ArgumentList.Add(url);
            Process.Start(psi);
            return;
        }
        catch
        {
            // Sem VLC — cai no handler do SO.
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            var psi = new ProcessStartInfo("xdg-open") { UseShellExecute = false };
            psi.ArgumentList.Add(url);
            Process.Start(psi);
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            var psi = new ProcessStartInfo("open") { UseShellExecute = false };
            psi.ArgumentList.Add(url);
            Process.Start(psi);
        }
    }
}
