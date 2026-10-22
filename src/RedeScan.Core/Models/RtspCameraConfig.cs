namespace RedeScan.Core.Models;

public class RtspCameraConfig
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "Câmera";
    public string Group { get; set; } = "Geral";
    public string StreamUrl { get; set; } = string.Empty;
    public string? Username { get; set; }
    public string? Password { get; set; }
    public bool IsEnabled { get; set; } = true;
    public int DisplayOrder { get; set; }

    /// <summary>
    /// Retorna a URL com credenciais injetadas se username/password estiverem preenchidos e não presentes na URL.
    /// </summary>
    public string GetAuthenticatedUrl()
    {
        if (string.IsNullOrWhiteSpace(StreamUrl))
            return string.Empty;

        if (string.IsNullOrWhiteSpace(Username))
            return StreamUrl;

        try
        {
            var uri = new Uri(StreamUrl);
            var userInfo = string.IsNullOrEmpty(Password) 
                ? Uri.EscapeDataString(Username) 
                : $"{Uri.EscapeDataString(Username)}:{Uri.EscapeDataString(Password)}";

            var builder = new UriBuilder(uri)
            {
                UserName = Username,
                Password = Password ?? string.Empty
            };
            return builder.Uri.ToString();
        }
        catch
        {
            return StreamUrl;
        }
    }
}
