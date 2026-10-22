namespace RedeScan.UI.Services;

/// <summary>Tipo de evento detectado pelo monitor de segundo plano.</summary>
public enum MonitorEventKind
{
    NetworkChanged,
    NewDevice
}

/// <summary>Notificação emitida pelo monitor (para exibir na bandeja).</summary>
public sealed record MonitorNotification(MonitorEventKind Kind, string Title, string Message);

/// <summary>
/// Monitora a rede em segundo plano: detecta mudança de rede (troca de
/// Wi-Fi/cabo, novo IP/gateway) e o aparecimento de novos dispositivos, emitindo
/// notificações. Fica ativo mesmo com a janela recolhida na bandeja.
/// </summary>
public interface IBackgroundMonitor
{
    bool IsRunning { get; }

    /// <summary>Disparado quando algo relevante muda na rede.</summary>
    event Action<MonitorNotification>? Notified;

    void Start();
    void Stop();
}
