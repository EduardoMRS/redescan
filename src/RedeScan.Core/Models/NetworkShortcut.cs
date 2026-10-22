namespace RedeScan.Core.Models;

public class NetworkShortcut
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = string.Empty;
    public string Target { get; set; } = string.Empty;
    public string Category { get; set; } = "Roteadores";
    public string? Description { get; set; }
}
