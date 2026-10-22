using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using RedeScan.Core.Models;
using RedeScan.UI.ViewModels;
using RedeScan.UI.Views;

namespace RedeScan.UI.Services;

/// <summary>
/// Abre o <see cref="CameraEditorWindow"/> como modal sobre a janela principal
/// (desktop). Em plataformas de view única (Android), onde não há janela-dono,
/// devolve a câmera inalterada para que a edição aconteça de outra forma.
/// </summary>
public sealed class CameraEditorDialog : ICameraEditorDialog
{
    public async Task<RtspCameraConfig?> EditAsync(RtspCameraConfig camera, bool isNew)
    {
        var owner = (Avalonia.Application.Current?.ApplicationLifetime
                        as IClassicDesktopStyleApplicationLifetime)?.MainWindow;

        var window = new CameraEditorWindow
        {
            DataContext = new CameraEditorViewModel(camera, isNew)
        };

        if (owner is not null)
        {
            return await window.ShowDialog<RtspCameraConfig?>(owner);
        }

        // Sem janela-dono (ex.: Android): mostra como janela comum não é
        // possível em single-view; retorna a câmera como está.
        return camera;
    }
}
