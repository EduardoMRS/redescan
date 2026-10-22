using System.Threading.Tasks;
using RedeScan.Core.Models;

namespace RedeScan.UI.Services;

/// <summary>
/// Abre o editor/criador de câmera em um modal. Recebe a câmera a editar (ou
/// uma nova instância para criação) e devolve a câmera com os dados confirmados
/// pelo usuário, ou <c>null</c> se o diálogo foi cancelado. O modal trabalha
/// sobre uma CÓPIA — o VM só aplica as mudanças ao receber o resultado.
/// </summary>
public interface ICameraEditorDialog
{
    Task<RtspCameraConfig?> EditAsync(RtspCameraConfig camera, bool isNew);
}
