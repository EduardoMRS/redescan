namespace RedeScan.Core.Services;

/// <summary>
/// Lookup de fabricante por OUI com uma base enxuta e curada dos prefixos mais
/// comuns em redes locais (roteadores, ONUs, câmeras, celulares e PCs). A base
/// pode ser estendida sem alterar a interface. Os prefixos ficam normalizados
/// em maiúsculas, sem separadores (ex.: "FCFBFB").
/// </summary>
public sealed class OuiVendorLookup : IOuiVendorLookup
{
    // Fonte: alocações públicas de OUI do IEEE (prefixos resumidos para uso em LAN).
    private static readonly IReadOnlyDictionary<string, string> Vendors = new Dictionary<string, string>
    {
        // Roteadores / rede
        ["FCFBFB"] = "Cisco",
        ["00000C"] = "Cisco",
        ["001018"] = "Broadcom",
        ["0018E7"] = "Cameo (TP-Link)",
        ["50C7BF"] = "TP-Link",
        ["A42BB0"] = "TP-Link",
        ["14CC20"] = "TP-Link",
        ["D8070B"] = "TP-Link",
        ["B0487A"] = "TP-Link",
        ["000E8F"] = "Sercomm",
        ["F81A67"] = "TP-Link",
        ["C46E1F"] = "TP-Link",
        ["0C8063"] = "TP-Link",
        ["E8DE27"] = "TP-Link",
        ["001A2B"] = "Ayecom (TP-Link)",
        ["002597"] = "Aerohive",
        ["60634C"] = "Intelbras",
        ["9C53CD"] = "Intelbras",
        ["3C8375"] = "Intelbras",
        ["E86F38"] = "Intelbras",
        ["F07D68"] = "D-Link",
        ["1C7EE5"] = "D-Link",
        ["0024A5"] = "Buffalo",
        ["002401"] = "D-Link",
        ["0022B0"] = "D-Link",
        ["244427"] = "ASUSTek",
        ["2CFDA1"] = "ASUSTek",
        ["04D4C4"] = "ASUSTek",
        ["3497F6"] = "ASUSTek",
        ["001599"] = "Samsung",
        ["F0989D"] = "Ubiquiti",
        ["788A20"] = "Ubiquiti",
        ["24A43C"] = "Ubiquiti",
        ["DC9FDB"] = "Ubiquiti",
        ["687251"] = "Ubiquiti",
        ["18E829"] = "Ubiquiti",
        ["0418D6"] = "Ubiquiti",
        ["002722"] = "Ubiquiti",
        ["44D9E7"] = "Ubiquiti",
        ["80EACA"] = "Dell",
        ["D43D7E"] = "Micro-Star (MSI)",
        ["7085C2"] = "ASRock",

        // Câmeras / CFTV
        ["00408C"] = "Axis Communications",
        ["ACCC8E"] = "Axis Communications",
        ["B8A44F"] = "Axis Communications",
        ["E0507B"] = "Hikvision",
        ["4CBD8F"] = "Hikvision",
        ["BCAD28"] = "Hikvision",
        ["C074AD"] = "Hikvision",
        ["44197C"] = "Hikvision",
        ["18680B"] = "Dahua",
        ["3C1F09"] = "Dahua",
        ["90021B"] = "Dahua",
        ["E45D52"] = "Dahua",
        ["BCF685"] = "Reolink",
        ["EC71DB"] = "Shenzhen (câmeras IP)",

        // Celulares / dispositivos pessoais
        ["F0C1F1"] = "Apple",
        ["3C0754"] = "Apple",
        ["A4D1D2"] = "Apple",
        ["ACBC32"] = "Apple",
        ["D0817A"] = "Apple",
        ["F4F15A"] = "Apple",
        ["5CF938"] = "Apple",
        ["B827EB"] = "Raspberry Pi Foundation",
        ["DCA632"] = "Raspberry Pi (Trading)",
        ["E45F01"] = "Raspberry Pi (Trading)",
        ["28CDC1"] = "Raspberry Pi (Trading)",
        ["D83ADD"] = "Raspberry Pi (Trading)",
        ["F8A2D6"] = "Xiaomi",
        ["64B473"] = "Xiaomi",
        ["286C07"] = "Xiaomi",
        ["50EC50"] = "Samsung",
        ["F025B7"] = "Samsung",
        ["8CF710"] = "Samsung",
        ["E8508B"] = "Huawei",
        ["00E0FC"] = "Huawei",
        ["48435A"] = "Huawei",
        ["ACE215"] = "Huawei",
        ["D8C771"] = "Motorola Mobility",
        ["F898B9"] = "Motorola Mobility",

        // Virtualização / fornecedores de VM
        ["000569"] = "VMware",
        ["000C29"] = "VMware",
        ["005056"] = "VMware",
        ["001C14"] = "VMware",
        ["080027"] = "VirtualBox (Oracle)",
        ["00155D"] = "Microsoft Hyper-V",
        ["525400"] = "QEMU/KVM",
    };

    public string? Lookup(string? macAddress)
    {
        if (string.IsNullOrWhiteSpace(macAddress))
        {
            return null;
        }

        var normalized = macAddress
            .Replace(":", string.Empty)
            .Replace("-", string.Empty)
            .Replace(".", string.Empty)
            .ToUpperInvariant();

        if (normalized.Length < 6)
        {
            return null;
        }

        var oui = normalized[..6];
        return Vendors.TryGetValue(oui, out var vendor) ? vendor : null;
    }
}
