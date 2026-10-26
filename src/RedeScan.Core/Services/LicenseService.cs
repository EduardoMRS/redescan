using System.Net.Http.Json;
using System.Net.NetworkInformation;
using System.Reflection;
using System.Text.Json.Serialization;
using RedeScan.Core.Diagnostics;
using RedeScan.Core.Models;

namespace RedeScan.Core.Services;

/// <summary>
/// Implementação de <see cref="ILicenseService"/>. Monta o payload de atividade
/// (versão do assembly, MAC da primeira interface física ativa e IP local) e faz
/// POST para a rota <c>/page/{projeto}/license</c> do portfólio.
///
/// O servidor resolve o IP público real a partir da conexão; o <c>ip</c> enviado
/// aqui é apenas informativo (IP local) e serve de fallback.
/// </summary>
public sealed class LicenseService : ILicenseService
{
    // Base do heartbeat no servidor de licenças (mesma origem do UpdateService).
    private const string HeartbeatUrl = "https://deveduardomrs.pro/page/redescan/license";

    private readonly HttpClient _http;

    public LicenseService(HttpClient? http = null)
    {
        _http = http ?? new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        if (!_http.DefaultRequestHeaders.UserAgent.Any())
        {
            _http.DefaultRequestHeaders.UserAgent.ParseAdd(
                $"RedeScan/{CurrentVersion} (+https://github.com/EduardoMRS/redescan)");
        }
    }

    public string CurrentVersion { get; } = ReadCurrentVersion();

    public async Task<LicenseHeartbeatResult> ReportActivityAsync(string? licenseCode = null, CancellationToken ct = default)
    {
        try
        {
            var payload = new LicenseHeartbeatPayload
            {
                Ip             = GetLocalIp(),
                Versao         = CurrentVersion,
                Mac            = GetPrimaryMacAddress(),
                CodigoLicenca  = string.IsNullOrWhiteSpace(licenseCode) ? null : licenseCode.Trim(),
            };

            using var response = await _http.PostAsJsonAsync(HeartbeatUrl, payload, ct);
            var status = (int)response.StatusCode;
            var ok = response.IsSuccessStatusCode;

            AppLog.Info(ok
                ? $"Heartbeat de licença enviado (HTTP {status}{(status == 201 ? ", licença reconhecida" : string.Empty)})."
                : $"Heartbeat de licença respondeu HTTP {status}.");

            return new LicenseHeartbeatResult
            {
                Success         = ok,
                StatusCode      = status,
                LicenseAccepted = status == 201,
                Error           = ok ? null : $"HTTP {status}",
            };
        }
        catch (Exception ex)
        {
            // Best-effort: nunca derruba o app por causa do heartbeat.
            AppLog.Warn("Falha ao enviar heartbeat de licença: " + ex.Message);
            return new LicenseHeartbeatResult
            {
                Success    = false,
                StatusCode = 0,
                Error      = ex.Message,
            };
        }
    }

    /// <summary>
    /// Obtém o MAC da primeira interface de rede física ativa (ignora loopback,
    /// túneis e interfaces desligadas). Retorna formato "AA:BB:CC:DD:EE:FF".
    /// </summary>
    private static string GetPrimaryMacAddress()
    {
        try
        {
            var nic = NetworkInterface.GetAllNetworkInterfaces()
                .Where(n => n.OperationalStatus == OperationalStatus.Up)
                .Where(n => n.NetworkInterfaceType is not NetworkInterfaceType.Loopback
                                                    and not NetworkInterfaceType.Tunnel)
                .Where(n => n.GetPhysicalAddress().GetAddressBytes().Length == 6)
                .OrderBy(n => n.NetworkInterfaceType == NetworkInterfaceType.Ethernet ? 0 : 1)
                .FirstOrDefault();

            var bytes = nic?.GetPhysicalAddress().GetAddressBytes();
            if (bytes is { Length: 6 })
            {
                return string.Join(":", bytes.Select(b => b.ToString("X2")));
            }
        }
        catch
        {
            // Sem permissão/plataforma sem NICs expostas — segue sem MAC.
        }

        return string.Empty;
    }

    /// <summary>IP local (IPv4) da primeira interface ativa; informativo apenas.</summary>
    private static string GetLocalIp()
    {
        try
        {
            foreach (var nic in NetworkInterface.GetAllNetworkInterfaces()
                         .Where(n => n.OperationalStatus == OperationalStatus.Up)
                         .Where(n => n.NetworkInterfaceType != NetworkInterfaceType.Loopback))
            {
                foreach (var ua in nic.GetIPProperties().UnicastAddresses)
                {
                    if (ua.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork
                        && !System.Net.IPAddress.IsLoopback(ua.Address))
                    {
                        return ua.Address.ToString();
                    }
                }
            }
        }
        catch
        {
            // ignora — o servidor usa o IP real da conexão de qualquer forma
        }

        return string.Empty;
    }

    private static string ReadCurrentVersion()
    {
        var asm  = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();
        var info = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        if (!string.IsNullOrWhiteSpace(info))
        {
            var plus = info.IndexOf('+');
            return plus > 0 ? info[..plus] : info;
        }
        return asm.GetName().Version?.ToString(3) ?? "0.0.0";
    }

    /// <summary>Corpo JSON do heartbeat enviado ao servidor.</summary>
    private sealed class LicenseHeartbeatPayload
    {
        [JsonPropertyName("ip")]
        public string? Ip { get; init; }

        [JsonPropertyName("versao")]
        public string? Versao { get; init; }

        [JsonPropertyName("mac")]
        public string? Mac { get; init; }

        [JsonPropertyName("codigo_licenca")]
        public string? CodigoLicenca { get; init; }
    }
}
