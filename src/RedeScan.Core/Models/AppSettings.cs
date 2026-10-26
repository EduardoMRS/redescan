namespace RedeScan.Core.Models;

public class AppSettings
{
    public List<RtspCameraConfig> Cameras { get; set; } = new();
    public List<NetworkShortcut> Shortcuts { get; set; } = new();
    public int DefaultPingTimeoutMs { get; set; } = 1000;
    public int DefaultPortTimeoutMs { get; set; } = 400;
    public int MaxScanConcurrency { get; set; } = 40;
    public string Theme { get; set; } = "Dark";

    // ------------------------------------------------- Preferências de UI
    /// <summary>
    /// Chave do módulo que deve abrir ao iniciar o app (ex.: "dashboard",
    /// "cameras"). Vazio = primeiro módulo ativo.
    /// </summary>
    public string StartupPageKey { get; set; } = "dashboard";

    /// <summary>
    /// Chaves dos módulos habilitados (exibidos no menu). Quando vazio, todos os
    /// módulos ficam visíveis. Permite esconder recursos que não serão usados.
    /// </summary>
    public List<string> EnabledModules { get; set; } = new();

    /// <summary>
    /// Página inicial do navegador embutido. Aceita uma URL ou o valor especial
    /// "router" para usar o gateway padrão da rede detectada.
    /// </summary>
    public string BrowserHomeUrl { get; set; } = "router";

    /// <summary>Porta inicial padrão do scan de portas.</summary>
    public int PortScanStart { get; set; } = 1;

    /// <summary>Porta final padrão do scan de portas.</summary>
    public int PortScanEnd { get; set; } = 1024;

    /// <summary>
    /// Aceita certificados HTTPS inválidos/autoassinados no navegador embutido
    /// (comum em roteadores). Desligado por padrão: reduz a segurança do HTTPS
    /// para todos os sites abertos no navegador embutido.
    /// </summary>
    public bool BrowserIgnoreCertificateErrors { get; set; }

    /// <summary>Intervalo de atualização do monitor de acessos (segundos).</summary>
    public int AccessMonitorIntervalSeconds { get; set; } = 2;

    /// <summary>
    /// Código de licença da instalação. Opcional: quando preenchido, é enviado
    /// no heartbeat de atividade (/page/{projeto}/license) e o servidor responde
    /// 201; quando vazio, o heartbeat é anônimo e o servidor responde 200.
    /// </summary>
    public string LicenseCode { get; set; } = string.Empty;

    public static AppSettings CreateDefault()
    {
        return new AppSettings
        {
            Shortcuts = new List<NetworkShortcut>
            {
                new() { Name = "TP-Link Padrão", Target = "192.168.0.1", Category = "Roteadores", Description = "IP gateway tradicional TP-Link" },
                new() { Name = "Mercusys / D-Link", Target = "192.168.1.1", Category = "Roteadores", Description = "IP gateway padrão Mercusys e D-Link" },
                new() { Name = "Intelbras Padrão", Target = "10.0.0.1", Category = "Roteadores", Description = "IP gateway tradicional Intelbras" },
                new() { Name = "Ubiquiti / ONU Resetada", Target = "192.168.1.20", Category = "Antenas & ONUs", Description = "IP de fábrica padrão Ubiquiti AirOS" },
                new() { Name = "ONU Custom Port", Target = "192.168.1.254:8090", Category = "Antenas & ONUs", Description = "Porta de gerência alternativa" },
                new() { Name = "Teste de Velocidade (Fast.com)", Target = "https://fast.com/pt/", Category = "Testes", Description = "Medidor de banda Netflix" },
                new() { Name = "YouTube", Target = "https://www.youtube.com/", Category = "Testes", Description = "Verificação de conectividade externa" }
            },
            Cameras = new List<RtspCameraConfig>()
        };
    }
}
