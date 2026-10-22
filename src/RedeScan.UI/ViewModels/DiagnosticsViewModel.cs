using System.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RedeScan.Core.Models;
using RedeScan.Core.Services;

namespace RedeScan.UI.ViewModels;

/// <summary>
/// Módulo 3 — ferramentas de diagnóstico: ping contínuo, traceroute e o
/// simulador de teste de carga (DDoS). Todas as operações são canceláveis.
/// </summary>
public partial class DiagnosticsViewModel : ViewModelBase
{
    private readonly IPingService _pingService;
    private readonly ILoadTestService _loadTestService;
    private CancellationTokenSource? _pingCts;
    private CancellationTokenSource? _traceCts;
    private CancellationTokenSource? _loadCts;

    public override string Title => "Diagnóstico";
    public override string Icon => "🩺";

    // ---- Ping contínuo ----
    [ObservableProperty]
    private string _pingTarget = "8.8.8.8";

    [ObservableProperty]
    private bool _isPinging;

    [ObservableProperty]
    private string _pingStats = "Nenhum teste executado.";

    public ObservableCollection<PingResult> PingHistory { get; } = new();

    /// <summary>Série de latências (ms) em ordem cronológica, para o gráfico.</summary>
    public ObservableCollection<double> LatencySeries { get; } = new();

    // ---- Traceroute ----
    [ObservableProperty]
    private string _tracerouteTarget = "8.8.8.8";

    [ObservableProperty]
    private bool _isTracing;

    public ObservableCollection<TracerouteHop> Hops { get; } = new();

    // ---- Simulador de carga ----
    public DdosAttackConfig LoadConfig { get; } = new();

    public LoadTestProtocol[] Protocols { get; } =
        (LoadTestProtocol[])Enum.GetValues(typeof(LoadTestProtocol));

    [ObservableProperty]
    private bool _isLoadTesting;

    [ObservableProperty]
    private DdosMetrics _loadMetrics = new();

    [ObservableProperty]
    private string _loadStatus = "Pronto. Use com responsabilidade — apenas em redes próprias.";

    private int _successCount;
    private int _failCount;
    private double _totalRtt;

    public DiagnosticsViewModel(IPingService pingService, ILoadTestService loadTestService)
    {
        _pingService = pingService;
        _loadTestService = loadTestService;
    }

    // ---------------------------------------------------------------- PING
    [RelayCommand]
    private async Task StartPingAsync()
    {
        if (IsPinging)
        {
            _pingCts?.Cancel();
            return;
        }

        if (string.IsNullOrWhiteSpace(PingTarget))
        {
            PingStats = "Informe um host ou IP.";
            return;
        }

        _pingCts = new CancellationTokenSource();
        var ct = _pingCts.Token;
        IsPinging = true;
        PingHistory.Clear();
        LatencySeries.Clear();
        _successCount = _failCount = 0;
        _totalRtt = 0;

        try
        {
            while (!ct.IsCancellationRequested)
            {
                var result = await _pingService.PingAsync(PingTarget.Trim(), ct: ct);
                PingHistory.Insert(0, result);
                if (PingHistory.Count > 200)
                {
                    PingHistory.RemoveAt(PingHistory.Count - 1);
                }

                if (result.Success)
                {
                    _successCount++;
                    _totalRtt += result.RoundtripTimeMs;
                    LatencySeries.Add(result.RoundtripTimeMs);
                }
                else
                {
                    _failCount++;
                    LatencySeries.Add(0);
                }

                if (LatencySeries.Count > 60)
                {
                    LatencySeries.RemoveAt(0);
                }

                UpdatePingStats();
                await Task.Delay(1000, ct);
            }
        }
        catch (OperationCanceledException)
        {
            // encerramento normal
        }
        finally
        {
            IsPinging = false;
            _pingCts?.Dispose();
            _pingCts = null;
        }
    }

    private void UpdatePingStats()
    {
        var total = _successCount + _failCount;
        var loss = total == 0 ? 0 : _failCount * 100.0 / total;
        var avg = _successCount == 0 ? 0 : _totalRtt / _successCount;
        PingStats = $"Enviados: {total}  |  OK: {_successCount}  |  Perda: {loss:F0}%  |  Média: {avg:F0} ms";
    }

    // ---------------------------------------------------------- TRACEROUTE
    [RelayCommand]
    private async Task TracerouteAsync()
    {
        if (IsTracing)
        {
            _traceCts?.Cancel();
            return;
        }

        if (string.IsNullOrWhiteSpace(TracerouteTarget))
        {
            return;
        }

        _traceCts = new CancellationTokenSource();
        IsTracing = true;
        Hops.Clear();

        var progress = new Progress<TracerouteHop>(hop => Hops.Add(hop));

        try
        {
            await _pingService.TracerouteAsync(
                TracerouteTarget.Trim(),
                progress: progress,
                ct: _traceCts.Token);
        }
        catch (OperationCanceledException)
        {
            // cancelado
        }
        finally
        {
            IsTracing = false;
            _traceCts?.Dispose();
            _traceCts = null;
        }
    }

    // --------------------------------------------------------- LOAD TEST
    [RelayCommand]
    private async Task StartLoadTestAsync()
    {
        if (IsLoadTesting)
        {
            _loadCts?.Cancel();
            return;
        }

        _loadCts = new CancellationTokenSource();
        IsLoadTesting = true;
        LoadStatus = $"Executando teste {LoadConfig.Protocol} contra {LoadConfig.TargetHost}:{LoadConfig.TargetPort}…";

        var progress = new Progress<DdosMetrics>(m => LoadMetrics = m);

        try
        {
            var final = await _loadTestService.RunLoadTestAsync(LoadConfig, progress, _loadCts.Token);
            LoadMetrics = final;
            LoadStatus = final.StatusMessage ?? "Teste concluído.";
        }
        catch (OperationCanceledException)
        {
            LoadStatus = "Teste de carga cancelado.";
        }
        catch (Exception ex)
        {
            LoadStatus = $"Erro no teste de carga: {ex.Message}";
        }
        finally
        {
            IsLoadTesting = false;
            _loadCts?.Dispose();
            _loadCts = null;
        }
    }
}
