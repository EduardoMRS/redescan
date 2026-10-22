using System.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RedeScan.Core.Models;
using RedeScan.Core.Services;

namespace RedeScan.UI.ViewModels;

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
    private AppSettings? _settings;

    public override string Title => "Câmeras RTSP";
    public override string Icon => "📷";

    public ObservableCollection<RtspCameraConfig> Cameras { get; } = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelection))]
    private RtspCameraConfig? _selectedCamera;

    [ObservableProperty]
    private string _status = "Carregando câmeras…";

    [ObservableProperty]
    private int _gridColumns = 2;

    public bool HasSelection => SelectedCamera is not null;

    public CamerasViewModel(IConfigurationService config)
    {
        _config = config;
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

        RecalculateGrid();
        Status = Cameras.Count == 0
            ? "Nenhuma câmera cadastrada. Clique em Adicionar."
            : $"{Cameras.Count} câmera(s) carregada(s).";
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
}
