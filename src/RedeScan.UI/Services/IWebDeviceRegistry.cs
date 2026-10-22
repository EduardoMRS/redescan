using System.Collections.ObjectModel;
using RedeScan.Core.Models;

namespace RedeScan.UI.Services;

/// <summary>
/// Registro compartilhado dos dispositivos com interface web descobertos na
/// rede (roteadores/ONUs e afins). O painel o alimenta ao descobrir; o
/// navegador embutido o consome para oferecer um atalho de navegação entre os
/// roteadores disponíveis.
/// </summary>
public interface IWebDeviceRegistry
{
    /// <summary>Dispositivos web conhecidos (observável para a UI).</summary>
    ReadOnlyObservableCollection<WebDevice> Devices { get; }

    /// <summary>Mescla um dispositivo descoberto, deduplicando por IP.</summary>
    void AddOrUpdate(WebDevice device);

    /// <summary>Substitui todo o conjunto conhecido.</summary>
    void Replace(IEnumerable<WebDevice> devices);
}
