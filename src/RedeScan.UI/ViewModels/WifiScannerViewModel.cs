using System.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RedeScan.Core.Models;
using RedeScan.Core.Services;

namespace RedeScan.UI.ViewModels;

/// <summary>
/// Módulo 4 — scanner de espectro Wi-Fi. Lista as redes visíveis com sinal,
/// canal e banda, destacando a rede conectada.
/// </summary>
public partial class WifiScannerViewModel : ViewModelBase
{
    private readonly IWifiScanner _wifiScanner;

    public override string Title => "Wi-Fi";
    public override string Icon => "📶";

    public ObservableCollection<WifiNetworkInfo> Networks { get; } = new();

    /// <summary>Ocupação por canal (contagem de redes), para o gráfico de colunas.</summary>
    public ObservableCollection<ChannelOccupancy> ChannelChart { get; } = new();

    [ObservableProperty]
    private bool _isScanning;

    [ObservableProperty]
    private bool _isSupported = true;

    [ObservableProperty]
    private string _status = "Clique em Escanear para listar as redes.";

    public WifiScannerViewModel(IWifiScanner wifiScanner)
    {
        _wifiScanner = wifiScanner;
    }

    [RelayCommand]
    private async Task ScanAsync(CancellationToken token)
    {
        IsScanning = true;
        Status = $"Escaneando via {_wifiScanner.PlatformName}…";
        try
        {
            IsSupported = await _wifiScanner.IsSupportedAsync(token);
            if (!IsSupported)
            {
                Status = "Nenhum adaptador Wi-Fi suportado foi detectado neste dispositivo.";
                Networks.Clear();
                return;
            }

            var found = await _wifiScanner.GetAvailableNetworksAsync(token);
            Networks.Clear();
            foreach (var net in found.OrderByDescending(n => n.SignalStrengthPercent))
            {
                Networks.Add(net);
            }

            BuildChannelChart(found);
            Status = $"{Networks.Count} rede(s) encontrada(s).";
        }
        catch (OperationCanceledException)
        {
            Status = "Escaneamento cancelado.";
        }
        catch (Exception ex)
        {
            Status = $"Erro ao escanear: {ex.Message}";
        }
        finally
        {
            IsScanning = false;
        }
    }

    private void BuildChannelChart(IReadOnlyList<WifiNetworkInfo> networks)
    {
        ChannelChart.Clear();

        var grouped = networks
            .Where(n => n.Channel > 0)
            .GroupBy(n => n.Channel)
            .OrderBy(g => g.Key)
            .Select(g => new ChannelOccupancy(
                g.Key,
                g.First().Band,
                g.Count()));

        foreach (var item in grouped)
        {
            ChannelChart.Add(item);
        }
    }
}

/// <summary>
/// Ocupação de um canal Wi-Fi: número de redes e banda. Usado pelo gráfico de
/// colunas que ajuda a identificar sobreposição/interferência.
/// </summary>
public sealed record ChannelOccupancy(int Channel, string Band, int NetworkCount);
