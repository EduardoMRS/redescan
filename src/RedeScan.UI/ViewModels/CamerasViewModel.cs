using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RedeScan.Core.Models;
using RedeScan.Core.Services;
using RedeScan.UI.Services;

namespace RedeScan.UI.ViewModels;

/// <summary>Um grupo de câmeras (por <see cref="RtspCameraConfig.Group"/>).</summary>
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
/// Um slot de exibição na grade de vídeo. Carrega a câmera atribuída (ou nenhuma)
/// e um índice fixo. A View assina <see cref="CamerasViewModel.SlotsChanged"/>
/// para (re)montar as superfícies de vídeo conforme estes slots.
/// </summary>
public partial class CameraSlot : ObservableObject
{
    public int Index { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasCamera))]
    [NotifyPropertyChangedFor(nameof(Label))]
    private RtspCameraConfig? _camera;

    public CameraSlot(int index) => Index = index;

    public bool HasCamera => Camera is not null;
    public string Label => Camera?.Name ?? $"Slot {Index + 1} (vazio)";
}

/// <summary>
/// Módulo de câmeras RTSP. Esquerda: tabela de câmeras com busca por nome e
/// filtro por grupo (+ miniatura de prévia). Direita: grade de vídeo em tempo
/// real configurável para 1 a 6 câmeras. O editor/criador abre em um modal.
/// As credenciais NUNCA ficam em código — vêm do perfil do usuário e são
/// injetadas na URL em runtime por <see cref="RtspCameraConfig.GetAuthenticatedUrl"/>.
/// </summary>
public partial class CamerasViewModel : ViewModelBase
{
    private readonly IConfigurationService _config;
    private readonly IRtspListImporter _rtspImporter;
    private readonly ICameraEditorDialog _editorDialog;
    private AppSettings? _settings;

    public override string Title => "Câmeras RTSP";
    public override string Icon => "📷";

    /// <summary>Todas as câmeras cadastradas (fonte de verdade).</summary>
    public ObservableCollection<RtspCameraConfig> Cameras { get; } = new();

    /// <summary>Câmeras após aplicar busca por nome e filtro por grupo (lista à esquerda).</summary>
    public ObservableCollection<RtspCameraConfig> FilteredCameras { get; } = new();

    /// <summary>Grupos disponíveis para o filtro (inclui "Todos os grupos").</summary>
    public ObservableCollection<string> AvailableGroups { get; } = new();

    /// <summary>Slots de vídeo ativos (1 a 6) exibidos na grade à direita.</summary>
    public ObservableCollection<CameraSlot> ActiveSlots { get; } = new();

    /// <summary>Opções de layout da grade de vídeo.</summary>
    public int[] LayoutOptions { get; } = { 1, 2, 3, 4, 5, 6 };

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelection))]
    private RtspCameraConfig? _selectedCamera;

    [ObservableProperty]
    private string _status = "Carregando câmeras…";

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private string _groupFilter = AllGroups;

    [ObservableProperty]
    private int _videoLayout = 1;

    [ObservableProperty]
    private int _gridColumns = 1;

    public bool HasSelection => SelectedCamera is not null;

    public const string AllGroups = "Todos os grupos";

    /// <summary>
    /// Pedido para iniciar/parar a reprodução nos slots. A View (com mídia
    /// nativa) assina estes eventos e controla os players LibVLC por slot.
    /// </summary>
    public event Action? SlotsChanged;
    public event Action<CameraSlot, RtspCameraConfig>? PlaySlotRequested;
    public event Action<CameraSlot>? StopSlotRequested;
    public event Action? StopAllRequested;

    public CamerasViewModel(
        IConfigurationService config,
        IRtspListImporter rtspImporter,
        ICameraEditorDialog editorDialog)
    {
        _config = config;
        _rtspImporter = rtspImporter;
        _editorDialog = editorDialog;
        RebuildSlots();
    }

    partial void OnSearchTextChanged(string value) => ApplyFilter();
    partial void OnGroupFilterChanged(string value) => ApplyFilter();
    partial void OnVideoLayoutChanged(int value) => RebuildSlots();

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
        ApplyFilter();
        Status = Cameras.Count == 0
            ? "Nenhuma câmera cadastrada. Clique em Adicionar ou Importar."
            : $"{Cameras.Count} câmera(s) em {Math.Max(0, AvailableGroups.Count - 1)} grupo(s).";
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

        // Mantém a seleção do filtro se ainda existir; senão volta para "Todos".
        GroupFilter = AvailableGroups.Contains(current) ? current : AllGroups;
    }

    /// <summary>Aplica busca por nome + filtro por grupo à lista da esquerda.</summary>
    private void ApplyFilter()
    {
        var term = SearchText?.Trim() ?? string.Empty;
        var group = GroupFilter;

        IEnumerable<RtspCameraConfig> query = Cameras;

        if (!string.IsNullOrEmpty(group) && group != AllGroups)
        {
            query = query.Where(c =>
                (string.IsNullOrWhiteSpace(c.Group) ? "Geral" : c.Group) == group);
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
            FilteredCameras.Add(cam);
        }
    }

    /// <summary>Monta a coleção de slots de acordo com o layout escolhido (1–6).</summary>
    private void RebuildSlots()
    {
        var count = Math.Clamp(VideoLayout, 1, 6);

        // Preserva as câmeras já atribuídas aos slots que continuam existindo.
        var existing = ActiveSlots.Select(s => s.Camera).ToList();

        ActiveSlots.Clear();
        for (var i = 0; i < count; i++)
        {
            var slot = new CameraSlot(i)
            {
                Camera = i < existing.Count ? existing[i] : null
            };
            ActiveSlots.Add(slot);
        }

        GridColumns = count switch
        {
            1 => 1,
            2 => 2,
            3 => 3,
            4 => 2,
            _ => 3   // 5 e 6 → 3 colunas
        };

        SlotsChanged?.Invoke();
    }

    /// <summary>Atribui a câmera selecionada ao primeiro slot livre (ou ao slot 0) e reproduz.</summary>
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

        var slot = ActiveSlots.FirstOrDefault(s => !s.HasCamera) ?? ActiveSlots.FirstOrDefault();
        if (slot is null)
        {
            return;
        }

        AssignAndPlay(slot, SelectedCamera);
    }

    /// <summary>Reproduz uma câmera num slot específico (usado pela View ao arrastar/duplo-clique).</summary>
    public void AssignAndPlay(CameraSlot slot, RtspCameraConfig camera)
    {
        slot.Camera = camera;
        PlaySlotRequested?.Invoke(slot, camera);
        Status = $"Reproduzindo \"{camera.Name}\" no slot {slot.Index + 1}.";
    }

    [RelayCommand]
    private void StopSlot(CameraSlot? slot)
    {
        if (slot is null)
        {
            return;
        }

        StopSlotRequested?.Invoke(slot);
        slot.Camera = null;
        Status = $"Slot {slot.Index + 1} parado.";
    }

    [RelayCommand]
    private void StopAll()
    {
        StopAllRequested?.Invoke();
        foreach (var s in ActiveSlots)
        {
            s.Camera = null;
        }
        Status = "Todos os streams foram parados.";
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
        SelectedCamera = result;
        RebuildGroups();
        ApplyFilter();
        Status = $"Câmera \"{result.Name}\" criada. Lembre de salvar.";
    }

    [RelayCommand]
    private async Task EditCameraAsync(RtspCameraConfig? camera)
    {
        camera ??= SelectedCamera;
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

        RebuildGroups();
        ApplyFilter();
        // Força atualização da seleção para refletir os novos campos na UI.
        SelectedCamera = null;
        SelectedCamera = result;
        Status = $"Câmera \"{result.Name}\" atualizada. Lembre de salvar.";
    }

    [RelayCommand]
    private void RemoveCamera(RtspCameraConfig? camera)
    {
        camera ??= SelectedCamera;
        if (camera is null)
        {
            return;
        }

        // Libera o slot que porventura exibia esta câmera.
        var slot = ActiveSlots.FirstOrDefault(s => ReferenceEquals(s.Camera, camera));
        if (slot is not null)
        {
            StopSlotRequested?.Invoke(slot);
            slot.Camera = null;
        }

        var name = camera.Name;
        Cameras.Remove(camera);
        if (ReferenceEquals(SelectedCamera, camera))
        {
            SelectedCamera = null;
        }

        RebuildGroups();
        ApplyFilter();
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

    // ------------------------------------------------------ Player externo
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
}
