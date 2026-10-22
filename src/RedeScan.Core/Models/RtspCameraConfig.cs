using System.Text.Json.Serialization;

namespace RedeScan.Core.Models;

/// <summary>
/// Configuração de uma câmera RTSP. O formato é compatível com o
/// <c>rtspList.json</c> do CentralMonitor (campos <see cref="Name"/>,
/// <see cref="Url"/>, <see cref="Location"/>, <see cref="Group"/>), permitindo
/// importar os mesmos arquivos diretamente. Os demais campos (Id, credenciais,
/// etc.) são extensões do RedeScan e ficam opcionais no JSON.
/// </summary>
public class RtspCameraConfig
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    public string Name { get; set; } = "Câmera";

    /// <summary>URL do stream RTSP. Compatível com o campo "Url" do CentralMonitor.</summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>Localização física (ex.: "Recepção"). Compatível com o CentralMonitor.</summary>
    public string Location { get; set; } = string.Empty;

    /// <summary>Grupo/host ao qual a câmera pertence. Usado para agrupar a grade.</summary>
    public string Group { get; set; } = "Geral";

    // --- Extensões do RedeScan (opcionais; não existem no CentralMonitor) ---
    public string? Username { get; set; }
    public string? Password { get; set; }
    public bool IsEnabled { get; set; } = true;
    public int DisplayOrder { get; set; }

    /// <summary>
    /// Alias de compatibilidade: versões anteriores do RedeScan usavam
    /// <c>StreamUrl</c>. Mantido para não quebrar configs já salvos — grava/lê
    /// em <see cref="Url"/>.
    /// </summary>
    [JsonIgnore]
    public string StreamUrl
    {
        get => Url;
        set => Url = value;
    }

    /// <summary>
    /// Retorna a URL com credenciais injetadas se username/password estiverem
    /// preenchidos. As credenciais nunca ficam em código — vêm do JSON do usuário.
    /// </summary>
    public string GetAuthenticatedUrl()
    {
        if (string.IsNullOrWhiteSpace(Url))
            return string.Empty;

        if (string.IsNullOrWhiteSpace(Username))
            return Url;

        try
        {
            var uri = new Uri(Url);
            var builder = new UriBuilder(uri)
            {
                UserName = Username,
                Password = Password ?? string.Empty
            };
            return builder.Uri.ToString();
        }
        catch
        {
            return Url;
        }
    }
}
