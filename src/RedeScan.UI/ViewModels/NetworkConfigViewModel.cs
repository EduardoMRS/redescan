using System.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RedeScan.Core.Models;
using RedeScan.Core.Services;

namespace RedeScan.UI.ViewModels;

/// <summary>
/// Configuração das interfaces de rede: IP estático/dinâmico (DHCP), DNS e
/// conexão PPPoE. Operações são sensíveis (exigem privilégio e mexem na rede
/// real do SO), então a UI sempre mostra o comando e só aplica após confirmação.
/// </summary>
public partial class NetworkConfigViewModel : ViewModelBase
{
    private readonly INetworkConfigurationService _netConfig;
    private readonly INetworkInterfaceService _interfaces;

    public override string Title => "Configuração de Rede";
    public override string Icon => "🛠️";

    public ObservableCollection<string> Interfaces { get; } = new();
    public string[] Modes { get; } = { "Dinâmico (DHCP)", "Estático" };

    [ObservableProperty] private string? _selectedInterface;
    [ObservableProperty] private int _selectedModeIndex;           // 0 = DHCP, 1 = estático
    [ObservableProperty] private string _addressCidr = "192.168.0.50/24";
    [ObservableProperty] private string _gateway = "192.168.0.1";
    [ObservableProperty] private string _dnsServers = "8.8.8.8, 1.1.1.1";

    // PPPoE
    [ObservableProperty] private string _pppoeUser = string.Empty;
    [ObservableProperty] private string _pppoePassword = string.Empty;

    [ObservableProperty] private string _backend = string.Empty;
    [ObservableProperty] private bool _isSupported;
    [ObservableProperty] private string _previewCommand = string.Empty;
    [ObservableProperty] private string _status = "Selecione uma interface e a ação desejada.";

    public bool IsStatic => SelectedModeIndex == 1;

    partial void OnSelectedModeIndexChanged(int value) => OnPropertyChanged(nameof(IsStatic));

    public NetworkConfigViewModel(
        INetworkConfigurationService netConfig,
        INetworkInterfaceService interfaces)
    {
        _netConfig = netConfig;
        _interfaces = interfaces;

        IsSupported = _netConfig.IsSupported;
        Backend = _netConfig.Backend;

        foreach (var (name, _, _) in _interfaces.GetAllActiveInterfaces())
        {
            Interfaces.Add(name);
        }
        SelectedInterface = Interfaces.FirstOrDefault();

        if (!IsSupported)
        {
            Status = "Configuração de rede não suportada nesta plataforma.";
        }
    }

    private IpConfigRequest BuildIpRequest() => new()
    {
        InterfaceName = SelectedInterface ?? string.Empty,
        Mode = IsStatic ? IpAssignmentMode.Static : IpAssignmentMode.Dynamic,
        AddressCidr = AddressCidr,
        Gateway = Gateway,
        DnsServers = ParseDns()
    };

    private List<string> ParseDns() => DnsServers
        .Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .ToList();

    // -------- Preview (não aplica nada) --------
    [RelayCommand]
    private async Task PreviewIpAsync(CancellationToken ct)
    {
        if (!Validate()) return;
        var r = await _netConfig.SetIpAsync(BuildIpRequest(), apply: false, ct);
        PreviewCommand = r.Command;
        Status = "Pré-visualização do comando (nada aplicado). Revise e clique em Aplicar.";
    }

    [RelayCommand]
    private async Task PreviewPppoeAsync(CancellationToken ct)
    {
        var r = await _netConfig.ConfigurePppoeAsync(BuildPppoe(), apply: false, ct);
        PreviewCommand = r.Command;
        Status = "Pré-visualização do PPPoE (a senha aparece mascarada; nada aplicado).";
    }

    // -------- Aplicar (ação destrutiva; exige privilégio) --------
    [RelayCommand]
    private async Task ApplyIpAsync(CancellationToken ct)
    {
        if (!Validate()) return;
        Status = "Aplicando configuração de IP…";
        var r = await _netConfig.SetIpAsync(BuildIpRequest(), apply: true, ct);
        ReportResult(r, "Configuração de IP");
    }

    [RelayCommand]
    private async Task ApplyDnsAsync(CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(SelectedInterface)) { Status = "Selecione uma interface."; return; }
        Status = "Aplicando DNS…";
        var r = await _netConfig.SetDnsAsync(SelectedInterface, ParseDns(), apply: true, ct);
        ReportResult(r, "DNS");
    }

    [RelayCommand]
    private async Task ApplyPppoeAsync(CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(PppoeUser)) { Status = "Informe o usuário PPPoE."; return; }
        Status = "Configurando PPPoE…";
        var r = await _netConfig.ConfigurePppoeAsync(BuildPppoe(), apply: true, ct);
        ReportResult(r, "PPPoE");
    }

    private PppoeRequest BuildPppoe() => new()
    {
        InterfaceName = SelectedInterface ?? string.Empty,
        Username = PppoeUser,
        Password = PppoePassword
    };

    private bool Validate()
    {
        if (string.IsNullOrWhiteSpace(SelectedInterface))
        {
            Status = "Selecione uma interface.";
            return false;
        }
        if (IsStatic && (string.IsNullOrWhiteSpace(AddressCidr) || string.IsNullOrWhiteSpace(Gateway)))
        {
            Status = "Para IP estático, informe endereço (CIDR) e gateway.";
            return false;
        }
        return true;
    }

    private void ReportResult(NetworkCommandResult r, string acao)
    {
        PreviewCommand = r.Command;
        if (r.Success)
        {
            Status = $"{acao} aplicada com sucesso.";
        }
        else
        {
            var detalhe = r.Error ?? "verifique se a aplicação foi executada com privilégio de administrador.";
            Status = $"Falha ao aplicar {acao}: {detalhe}";
        }
    }
}
