using CommunityToolkit.Mvvm.ComponentModel;
using RedeScan.Core.Models;

namespace RedeScan.UI.ViewModels;

/// <summary>
/// ViewModel do modal de edição/criação de câmera. Trabalha sobre os campos
/// editáveis isoladamente; o <see cref="BuildResult"/> monta a câmera final
/// quando o usuário confirma.
/// </summary>
public partial class CameraEditorViewModel : ObservableObject
{
    private readonly RtspCameraConfig _original;

    public CameraEditorViewModel(RtspCameraConfig camera, bool isNew)
    {
        _original = camera;
        IsNew = isNew;
        Name = camera.Name;
        Group = camera.Group;
        Location = camera.Location;
        Url = camera.Url;
        Username = camera.Username;
        Password = camera.Password;
        IsEnabled = camera.IsEnabled;
    }

    public bool IsNew { get; }

    public string WindowTitle => IsNew ? "Nova câmera" : "Editar câmera";

    [ObservableProperty] private string _name = "Câmera";
    [ObservableProperty] private string _group = "Geral";
    [ObservableProperty] private string _location = string.Empty;
    [ObservableProperty] private string _url = string.Empty;
    [ObservableProperty] private string? _username;
    [ObservableProperty] private string? _password;
    [ObservableProperty] private bool _isEnabled = true;

    /// <summary>Aplica os campos editados de volta na instância original e a devolve.</summary>
    public RtspCameraConfig BuildResult()
    {
        _original.Name = string.IsNullOrWhiteSpace(Name) ? "Câmera" : Name.Trim();
        _original.Group = string.IsNullOrWhiteSpace(Group) ? "Geral" : Group.Trim();
        _original.Location = Location?.Trim() ?? string.Empty;
        _original.Url = Url?.Trim() ?? string.Empty;
        _original.Username = string.IsNullOrWhiteSpace(Username) ? null : Username.Trim();
        _original.Password = string.IsNullOrWhiteSpace(Password) ? null : Password;
        _original.IsEnabled = IsEnabled;
        return _original;
    }
}
