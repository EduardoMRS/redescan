using System.Net;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RedeScan.Core.Services;

namespace RedeScan.UI.ViewModels;

/// <summary>
/// Painel inicial com um resumo das informações da rede local detectadas
/// pelo <see cref="INetworkInterfaceService"/>.
/// </summary>
public partial class DashboardViewModel : ViewModelBase
{
    private readonly INetworkInterfaceService _networkInterface;

    public override string Title => "Painel";
    public override string Icon => "🏠";

    [ObservableProperty]
    private string _localIp = "—";

    [ObservableProperty]
    private string _gateway = "—";

    [ObservableProperty]
    private string _subnetPrefix = "—";

    [ObservableProperty]
    private string _hostName = Dns.GetHostName();

    public ObservableCollection<string> ActiveInterfaces { get; } = new();

    public DashboardViewModel(INetworkInterfaceService networkInterface)
    {
        _networkInterface = networkInterface;
        Refresh();
    }

    [RelayCommand]
    private void Refresh()
    {
        LocalIp = _networkInterface.GetPrimaryLocalIpAddress()?.ToString() ?? "indisponível";
        Gateway = _networkInterface.GetDefaultGateway()?.ToString() ?? "indisponível";
        SubnetPrefix = _networkInterface.GetSubnetPrefix() is { } prefix
            ? $"{prefix.TrimEnd('.')}.0/24"
            : "indisponível";

        ActiveInterfaces.Clear();
        foreach (var (name, ip, gw) in _networkInterface.GetAllActiveInterfaces())
        {
            ActiveInterfaces.Add($"{name}  —  {ip}" + (gw is not null ? $"  (gw {gw})" : string.Empty));
        }
    }
}
