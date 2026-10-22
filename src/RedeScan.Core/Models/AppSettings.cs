namespace RedeScan.Core.Models;

public class AppSettings
{
    public List<RtspCameraConfig> Cameras { get; set; } = new();
    public List<NetworkShortcut> Shortcuts { get; set; } = new();
    public int DefaultPingTimeoutMs { get; set; } = 1000;
    public int DefaultPortTimeoutMs { get; set; } = 400;
    public int MaxScanConcurrency { get; set; } = 40;
    public string Theme { get; set; } = "Dark";

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
