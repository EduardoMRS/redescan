using System.Globalization;
using System.Text;
using System.Threading;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RedeScan.Core.Diagnostics;
using RedeScan.Core.Models;
using RedeScan.Core.Services;

namespace RedeScan.UI.ViewModels;

/// <summary>Um evento no histórico do monitor: conexão nova vista em determinado instante.</summary>
public sealed record AccessEvent(DateTime Time, ConnectionEntry Entry)
{
    public string TimeLabel => Time.ToString("HH:mm:ss", CultureInfo.InvariantCulture);
}

/// <summary>Resumo por IP remoto: quantas conexões e em quais portas desta máquina.</summary>
public sealed record RemoteSummary(string Address, int Connections, string Ports, DateTime LastSeen)
{
    public string LastSeenLabel => LastSeen.ToString("HH:mm:ss", CultureInfo.InvariantCulture);
}

/// <summary>
/// Monitor de acessos desta máquina: lista ao vivo as conexões TCP/UDP e as
/// portas em escuta (com o processo responsável), registra cada conexão nova
/// num histórico com horário e resume quais IPs acessaram quais portas.
/// </summary>
public partial class AccessMonitorViewModel : ViewModelBase, IDisposable
{
    private readonly IConnectionMonitorService _monitor;
    private readonly IConfigurationService _config;
    private CancellationTokenSource? _loopCts;
    private HashSet<string> _known = new();
    private IReadOnlyList<ConnectionEntry> _last = Array.Empty<ConnectionEntry>();
    private readonly Dictionary<string, (int Count, HashSet<int> Ports, DateTime Last)> _byRemote = new();

    public override string Title => "Monitor de Acessos";
    public override string Icon => "🛰️";
    public override string Key => "accessmonitor";

    /// <summary>Conexões atuais (após filtros).</summary>
    public ObservableCollection<ConnectionEntry> Connections { get; } = new();

    /// <summary>Histórico de conexões novas desde que o monitor foi iniciado.</summary>
    public ObservableCollection<AccessEvent> Events { get; } = new();

    /// <summary>IPs remotos que acessaram esta máquina (entradas).</summary>
    public ObservableCollection<RemoteSummary> Remotes { get; } = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsStopped))]
    private bool _isRunning;

    [ObservableProperty] private string _filterText = string.Empty;
    [ObservableProperty] private bool _onlyInbound;
    [ObservableProperty] private bool _hideListening;
    [ObservableProperty] private bool _hideLoopback = true;
    [ObservableProperty] private string _status = "Clique em Iniciar para monitorar os acessos desta máquina.";
    [ObservableProperty] private string _summary = string.Empty;
    [ObservableProperty] private string _backend = string.Empty;

    public bool IsStopped => !IsRunning;

    /// <summary>Aviso sobre privilégios: sem root/admin, alguns processos aparecem sem nome.</summary>
    public string PrivilegeHint =>
        Environment.UserName is "root" || IsWindowsAdmin()
            ? "Executando com privilégios: todos os processos são identificados."
            : "Sem privilégios de administrador, alguns processos aparecem sem nome. Para ver todos, execute o RedeScan como administrador (ex.: sudo redescan).";

    public AccessMonitorViewModel(IConnectionMonitorService monitor, IConfigurationService config)
    {
        _monitor = monitor;
        _config = config;
    }

    partial void OnFilterTextChanged(string value) => ApplyFilter();
    partial void OnOnlyInboundChanged(bool value) => ApplyFilter();
    partial void OnHideListeningChanged(bool value) => ApplyFilter();
    partial void OnHideLoopbackChanged(bool value) => ApplyFilter();

    [RelayCommand]
    private void Start()
    {
        if (IsRunning)
        {
            return;
        }

        IsRunning = true;
        _loopCts = new CancellationTokenSource();
        var interval = TimeSpan.FromSeconds(Math.Clamp(_config.LoadSettings().AccessMonitorIntervalSeconds, 1, 30));
        Status = $"Monitorando a cada {interval.TotalSeconds:0}s…";
        _ = LoopAsync(interval, _loopCts.Token);
    }

    [RelayCommand]
    private void Stop()
    {
        _loopCts?.Cancel();
        _loopCts = null;
        IsRunning = false;
        Status = "Monitor parado.";
    }

    [RelayCommand]
    private void ClearHistory()
    {
        Events.Clear();
        _byRemote.Clear();
        Remotes.Clear();
        Status = "Histórico limpo.";
    }

    private async Task LoopAsync(TimeSpan interval, CancellationToken ct)
    {
        var first = true;
        while (!ct.IsCancellationRequested)
        {
            try
            {
                var snapshot = await _monitor.SnapshotAsync(ct);
                await Dispatcher.UIThread.InvokeAsync(() => Apply(snapshot, first));
                first = false;
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                AppLog.Error("Monitor de acessos falhou", ex);
                Status = "Erro ao ler conexões: " + ex.Message;
            }

            try
            {
                await Task.Delay(interval, ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private void Apply(IReadOnlyList<ConnectionEntry> snapshot, bool first)
    {
        Backend = _monitor.Backend;
        _last = snapshot;
        var now = DateTime.Now;
        var keys = new HashSet<string>(snapshot.Count);

        foreach (var entry in snapshot)
        {
            keys.Add(entry.Key);
            var isNew = !_known.Contains(entry.Key);
            if (!isNew || entry.Direction == ConnectionDirection.Listening)
            {
                continue;
            }

            // No primeiro ciclo só registra o estado base, sem "eventos novos".
            if (!first)
            {
                Events.Insert(0, new AccessEvent(now, entry));
                if (Events.Count > 2000)
                {
                    Events.RemoveAt(Events.Count - 1);
                }
            }

            if (entry.Direction == ConnectionDirection.Inbound && !entry.IsLoopback)
            {
                var (count, ports, _) = _byRemote.TryGetValue(entry.RemoteAddress, out var cur)
                    ? cur
                    : (0, new HashSet<int>(), now);
                ports.Add(entry.LocalPort);
                _byRemote[entry.RemoteAddress] = (count + 1, ports, now);
            }
        }

        _known = keys;
        RebuildRemotes();
        ApplyFilter();

        var inbound = snapshot.Count(e => e.Direction == ConnectionDirection.Inbound);
        var outbound = snapshot.Count(e => e.Direction == ConnectionDirection.Outbound);
        var listening = snapshot.Count(e => e.Direction == ConnectionDirection.Listening);
        Summary = $"{inbound} entrada(s) · {outbound} saída(s) · {listening} porta(s) em escuta · {Events.Count} evento(s)";
        Status = $"Atualizado às {now:HH:mm:ss} (fonte: {Backend}).";
    }

    private void RebuildRemotes()
    {
        Remotes.Clear();
        foreach (var (addr, info) in _byRemote.OrderByDescending(kv => kv.Value.Last))
        {
            Remotes.Add(new RemoteSummary(addr, info.Count, string.Join(", ", info.Ports.OrderBy(p => p)), info.Last));
        }
    }

    private void ApplyFilter()
    {
        var term = FilterText?.Trim() ?? string.Empty;
        IEnumerable<ConnectionEntry> q = _last;

        if (OnlyInbound)
        {
            q = q.Where(e => e.Direction == ConnectionDirection.Inbound);
        }

        if (HideListening)
        {
            q = q.Where(e => e.Direction != ConnectionDirection.Listening);
        }

        if (HideLoopback)
        {
            q = q.Where(e => !e.IsLoopback);
        }

        if (term.Length > 0)
        {
            q = q.Where(e =>
                e.Local.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                e.Remote.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                e.Process.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                e.State.Contains(term, StringComparison.OrdinalIgnoreCase));
        }

        var ordered = q
            .OrderBy(e => e.Direction switch
            {
                ConnectionDirection.Inbound => 0,
                ConnectionDirection.Outbound => 1,
                ConnectionDirection.Listening => 2,
                _ => 3
            })
            .ThenBy(e => e.LocalPort)
            .ToList();

        Connections.Clear();
        foreach (var e in ordered)
        {
            Connections.Add(e);
        }
    }

    /// <summary>Exporta o histórico de eventos em CSV (para auditoria/planilha).</summary>
    [RelayCommand]
    private async Task ExportCsvAsync(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        var sb = new StringBuilder("horario;sentido;protocolo;local;remoto;estado;processo\n");
        foreach (var ev in Events.Reverse())
        {
            var e = ev.Entry;
            sb.Append(ev.Time.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)).Append(';')
              .Append(e.DirectionLabel.Replace("⬇ ", string.Empty).Replace("⬆ ", string.Empty)).Append(';')
              .Append(e.Protocol).Append(';').Append(e.Local).Append(';').Append(e.Remote).Append(';')
              .Append(e.State).Append(';').Append(Csv(e.Process)).Append('\n');
        }

        await File.WriteAllTextAsync(path, sb.ToString(), Encoding.UTF8);
        Status = $"{Events.Count} evento(s) exportado(s) para {Path.GetFileName(path)}.";
    }

    private static string Csv(string value) =>
        value.Contains(';') || value.Contains('"') ? "\"" + value.Replace("\"", "\"\"") + "\"" : value;

    private static bool IsWindowsAdmin()
    {
        if (!OperatingSystem.IsWindows())
        {
            return false;
        }

        try
        {
            using var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
            return new System.Security.Principal.WindowsPrincipal(identity)
                .IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
        }
        catch
        {
            return false;
        }
    }

    public void Dispose()
    {
        _loopCts?.Cancel();
        _loopCts?.Dispose();
    }
}
