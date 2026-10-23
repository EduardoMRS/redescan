namespace RedeScan.Core.Models;

/// <summary>Sentido de uma conexão em relação a esta máquina.</summary>
public enum ConnectionDirection
{
    /// <summary>Porta aberta aguardando conexões (servidor local).</summary>
    Listening,

    /// <summary>Alguém de fora acessou um serviço desta máquina.</summary>
    Inbound,

    /// <summary>Esta máquina iniciou a conexão para outro host.</summary>
    Outbound,

    /// <summary>Não foi possível determinar (ex.: UDP sem estado).</summary>
    Unknown
}

/// <summary>Uma conexão de rede (ou porta em escuta) observada nesta máquina.</summary>
public sealed record ConnectionEntry
{
    public string Protocol { get; init; } = "TCP";
    public string LocalAddress { get; init; } = string.Empty;
    public int LocalPort { get; init; }
    public string RemoteAddress { get; init; } = string.Empty;
    public int RemotePort { get; init; }
    public string State { get; init; } = string.Empty;
    public int? ProcessId { get; init; }
    public string? ProcessName { get; init; }
    public ConnectionDirection Direction { get; init; } = ConnectionDirection.Unknown;

    /// <summary>Chave estável para comparar instantâneos (detectar conexões novas).</summary>
    public string Key => $"{Protocol}|{LocalAddress}:{LocalPort}|{RemoteAddress}:{RemotePort}";

    public string Local => $"{FormatAddress(LocalAddress)}:{LocalPort}";
    public string Remote => RemotePort == 0 && IsAnyAddress(RemoteAddress) ? "—" : $"{FormatAddress(RemoteAddress)}:{RemotePort}";

    public string Process => ProcessName is null
        ? (ProcessId is { } pid ? $"PID {pid}" : "—")
        : ProcessId is { } p ? $"{ProcessName} ({p})" : ProcessName;

    public string DirectionLabel => Direction switch
    {
        ConnectionDirection.Listening => "Escutando",
        ConnectionDirection.Inbound => "⬇ Entrada",
        ConnectionDirection.Outbound => "⬆ Saída",
        _ => "?"
    };

    /// <summary>True se o endereço remoto é loopback (127.x / ::1).</summary>
    public bool IsLoopback =>
        IsLoopbackAddress(RemoteAddress) ||
        (Direction == ConnectionDirection.Listening && IsLoopbackAddress(LocalAddress));

    private static bool IsLoopbackAddress(string address)
    {
        // IPv4 mapeado em IPv6 (::ffff:127.0.0.1) também é loopback.
        var a = address.StartsWith("::ffff:", StringComparison.OrdinalIgnoreCase) ? address[7..] : address;
        return a.StartsWith("127.", StringComparison.Ordinal) || a is "::1" or "localhost";
    }

    public static bool IsAnyAddress(string address) =>
        address is "" or "*" or "0.0.0.0" or "::" or "[::]";

    private static string FormatAddress(string address) =>
        address.Contains(':') && !address.StartsWith('[') ? $"[{address}]" : address;
}
