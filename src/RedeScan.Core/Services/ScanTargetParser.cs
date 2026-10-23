using System.Globalization;
using System.Net;
using System.Net.Sockets;

namespace RedeScan.Core.Services;

/// <summary>Resultado da interpretação dos alvos do scanner.</summary>
public sealed record ScanTargets
{
    /// <summary>IPs a varrer, sem duplicados, em ordem numérica.</summary>
    public IReadOnlyList<IPAddress> Addresses { get; init; } = Array.Empty<IPAddress>();

    /// <summary>Faixas reconhecidas, já normalizadas (ex.: "192.168.0.0/24").</summary>
    public IReadOnlyList<string> Ranges { get; init; } = Array.Empty<string>();

    /// <summary>Trechos que não puderam ser interpretados.</summary>
    public IReadOnlyList<string> Errors { get; init; } = Array.Empty<string>();

    /// <summary>True se a lista foi cortada no limite de hosts.</summary>
    public bool Truncated { get; init; }

    public bool IsValid => Addresses.Count > 0 && Errors.Count == 0;
}

/// <summary>
/// Interpreta alvos de varredura IPv4 digitados pelo usuário. Aceita vários
/// itens separados por vírgula, ponto e vírgula, espaço ou quebra de linha:
/// <list type="bullet">
/// <item>CIDR: <c>192.168.0.0/24</c>, <c>10.0.1.1/23</c> (normaliza para a rede).</item>
/// <item>Prefixo: <c>192.168.0</c> ou <c>192.168.0.</c> = /24; <c>10.0</c> = /16.</item>
/// <item>Faixa: <c>10.1.0.1-10.1.1.10</c> ou abreviada <c>192.168.0.10-50</c>.</item>
/// <item>IP único: <c>192.168.0.1</c>.</item>
/// </list>
/// Em redes /30 ou maiores, o endereço de rede e o de broadcast são omitidos.
/// </summary>
public static class ScanTargetParser
{
    /// <summary>Limite padrão de hosts por varredura (equivale a uma /16).</summary>
    public const int DefaultMaxHosts = 65_536;

    private static readonly char[] Separators = { ',', ';', ' ', '\t', '\r', '\n' };

    public static ScanTargets Parse(string? input, int maxHosts = DefaultMaxHosts)
    {
        var set = new SortedSet<uint>();
        var ranges = new List<string>();
        var errors = new List<string>();
        var truncated = false;

        foreach (var raw in (input ?? string.Empty).Split(Separators, StringSplitOptions.RemoveEmptyEntries))
        {
            var token = raw.Trim();
            if (!TryParseToken(token, out var first, out var last, out var label, out var error))
            {
                errors.Add(error is null ? token : $"{token} ({error})");
                continue;
            }

            ranges.Add(label);
            for (var ip = (ulong)first; ip <= last; ip++)
            {
                if (set.Count >= maxHosts)
                {
                    truncated = true;
                    break;
                }

                set.Add((uint)ip);
            }
        }

        return new ScanTargets
        {
            Addresses = set.Select(ToAddress).ToList(),
            Ranges = ranges,
            Errors = errors,
            Truncated = truncated
        };
    }

    /// <summary>Converte um item em [primeiro, último] IP (inclusivo) já sem rede/broadcast.</summary>
    public static bool TryParseToken(string token, out uint first, out uint last, out string label, out string? error)
    {
        first = last = 0;
        label = token;
        error = null;

        // CIDR
        var slash = token.IndexOf('/');
        if (slash >= 0)
        {
            if (!TryParseIp(token[..slash], out var ip) ||
                !int.TryParse(token[(slash + 1)..], NumberStyles.None, CultureInfo.InvariantCulture, out var bits) ||
                bits is < 0 or > 32)
            {
                error = "CIDR inválido";
                return false;
            }

            var mask = bits == 0 ? 0u : uint.MaxValue << (32 - bits);
            var network = ip & mask;
            var broadcast = network | ~mask;
            label = $"{ToAddress(network)}/{bits}";
            (first, last) = bits <= 30 ? (network + 1, broadcast - 1) : (network, broadcast);
            return true;
        }

        // Faixa a-b (b pode ser IP completo ou só o último octeto)
        var dash = token.IndexOf('-');
        if (dash >= 0)
        {
            var left = token[..dash];
            var right = token[(dash + 1)..];
            if (!TryParseIp(left, out var start))
            {
                error = "início da faixa inválido";
                return false;
            }

            uint end;
            if (TryParseIp(right, out var full))
            {
                end = full;
            }
            else if (byte.TryParse(right, NumberStyles.None, CultureInfo.InvariantCulture, out var lastOctet))
            {
                end = (start & 0xFFFFFF00u) | lastOctet;
            }
            else
            {
                error = "fim da faixa inválido";
                return false;
            }

            if (end < start)
            {
                error = "o fim é menor que o início";
                return false;
            }

            (first, last) = (start, end);
            label = $"{ToAddress(start)}-{ToAddress(end)}";
            return true;
        }

        // Prefixo (1 a 3 octetos, com ou sem ponto final) ou IP completo
        var parts = token.TrimEnd('.').Split('.');
        if (parts.Length is >= 1 and <= 4 && parts.All(p => byte.TryParse(p, NumberStyles.None, CultureInfo.InvariantCulture, out _)))
        {
            if (parts.Length == 4)
            {
                TryParseIp(token, out first);
                last = first;
                label = ToAddress(first).ToString();
                return true;
            }

            if (parts.Length == 1)
            {
                error = "prefixo curto demais (use ao menos 2 octetos, ex.: 10.0)";
                return false;
            }

            var bits = parts.Length * 8;
            var padded = string.Join('.', parts.Concat(Enumerable.Repeat("0", 4 - parts.Length)));
            return TryParseToken($"{padded}/{bits}", out first, out last, out label, out error);
        }

        error = "formato não reconhecido";
        return false;
    }

    private static bool TryParseIp(string text, out uint value)
    {
        value = 0;
        var parts = text.Split('.');
        if (parts.Length != 4 ||
            !IPAddress.TryParse(text, out var ip) ||
            ip.AddressFamily != AddressFamily.InterNetwork)
        {
            return false;
        }

        var b = ip.GetAddressBytes();
        value = (uint)(b[0] << 24 | b[1] << 16 | b[2] << 8 | b[3]);
        return true;
    }

    private static IPAddress ToAddress(uint value) =>
        new(new[] { (byte)(value >> 24), (byte)(value >> 16), (byte)(value >> 8), (byte)value });
}
