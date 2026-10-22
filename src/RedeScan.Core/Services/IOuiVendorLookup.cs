namespace RedeScan.Core.Services;

/// <summary>
/// Traduz o prefixo OUI (os 3 primeiros octetos de um MAC) no nome do
/// fabricante do equipamento.
/// </summary>
public interface IOuiVendorLookup
{
    /// <summary>
    /// Retorna o fabricante correspondente ao MAC informado, ou <c>null</c>
    /// quando o prefixo não é conhecido. Aceita MACs com ':' ou '-'.
    /// </summary>
    string? Lookup(string? macAddress);
}
