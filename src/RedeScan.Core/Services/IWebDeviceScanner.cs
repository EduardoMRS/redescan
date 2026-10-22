using RedeScan.Core.Models;

namespace RedeScan.Core.Services;

/// <summary>
/// Descobre dispositivos na sub-rede local que expõem interface web (em qualquer
/// porta comum), para oferecer atalhos rápidos no painel.
/// </summary>
public interface IWebDeviceScanner
{
    /// <summary>Portas consideradas "web" ao procurar dispositivos.</summary>
    IReadOnlyList<int> WebPorts { get; }

    /// <summary>
    /// Varre a sub-rede (via <paramref name="subnetPrefix"/> no formato "a.b.c.")
    /// e retorna os hosts com ao menos uma porta web aberta. Reporta cada
    /// dispositivo encontrado ao vivo por <paramref name="progress"/>.
    /// </summary>
    Task<IReadOnlyList<WebDevice>> ScanAsync(
        string subnetPrefix,
        IProgress<WebDevice>? progress = null,
        CancellationToken ct = default);

    /// <summary>
    /// Procura rapidamente possíveis roteadores: testa IPs de gateway comuns
    /// (ex.: .1, .254) e os gateways padrão das interfaces ativas.
    /// </summary>
    Task<IReadOnlyList<WebDevice>> FindRoutersAsync(CancellationToken ct = default);
}
