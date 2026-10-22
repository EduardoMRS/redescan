using System.Text;
using System.Threading;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RedeScan.Core.Services;

namespace RedeScan.UI.ViewModels;

/// <summary>
/// Terminal SSH embutido, disponível em todas as plataformas (inclusive
/// Android). Usa <see cref="ISshTerminalSession"/> (SSH.NET) — um shell de
/// linha/stream, adequado para CLI de roteadores/switches. Não é um emulador
/// VT100 completo.
/// </summary>
public partial class TerminalViewModel : ViewModelBase
{
    private readonly ISshTerminalFactory _factory;
    private ISshTerminalSession? _session;
    private readonly StringBuilder _buffer = new();

    public override string Title => "Terminal SSH";
    public override string Icon => "⌨️";

    [ObservableProperty] private string _host = string.Empty;
    [ObservableProperty] private int _port = 22;
    [ObservableProperty] private string _username = string.Empty;
    [ObservableProperty] private string _password = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsDisconnected))]
    private bool _isConnected;

    [ObservableProperty] private string _output = string.Empty;
    [ObservableProperty] private string _commandInput = string.Empty;
    [ObservableProperty] private string _status = "Informe host, usuário e senha para conectar via SSH.";

    public bool IsDisconnected => !IsConnected;

    public TerminalViewModel(ISshTerminalFactory factory)
    {
        _factory = factory;
    }

    /// <summary>Pré-preenche a conexão (usado ao abrir a partir do scanner).</summary>
    public void Prepare(string host, int port = 22, string? username = null)
    {
        Host = host;
        Port = port;
        if (!string.IsNullOrWhiteSpace(username))
        {
            Username = username;
        }
        Status = $"Pronto para conectar em {host}:{port}.";
    }

    [RelayCommand]
    private async Task ConnectAsync()
    {
        if (IsConnected)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(Host) || string.IsNullOrWhiteSpace(Username))
        {
            Status = "Informe ao menos host e usuário.";
            return;
        }

        _session = _factory.Create();
        _session.OutputReceived += OnOutput;
        _session.Disconnected += OnDisconnected;

        Status = $"Conectando em {Host}:{Port}…";
        try
        {
            await _session.ConnectAsync(new SshConnectionInfo
            {
                Host = Host.Trim(),
                Port = Port,
                Username = Username.Trim(),
                Password = Password
            });

            IsConnected = true;
            Status = $"Conectado a {Host}:{Port}.";
        }
        catch (Exception ex)
        {
            Status = $"Falha ao conectar: {ex.Message}";
            CleanupSession();
        }
    }

    [RelayCommand]
    private async Task SendAsync()
    {
        if (_session is null || !IsConnected)
        {
            return;
        }

        // Envia o comando com quebra de linha (como pressionar Enter).
        var cmd = CommandInput;
        CommandInput = string.Empty;
        await _session.SendAsync(cmd + "\n");
    }

    [RelayCommand]
    private void Disconnect()
    {
        _session?.Disconnect();
        Status = "Desconectado.";
    }

    [RelayCommand]
    private void ClearOutput()
    {
        _buffer.Clear();
        Output = string.Empty;
    }

    private void OnOutput(string text)
    {
        // Evento vem de thread de background: marshala para a UI.
        Dispatcher.UIThread.Post(() =>
        {
            _buffer.Append(text);
            // Limita o histórico para não crescer indefinidamente.
            if (_buffer.Length > 200_000)
            {
                _buffer.Remove(0, _buffer.Length - 150_000);
            }
            Output = _buffer.ToString();
        });
    }

    private void OnDisconnected(string? error)
    {
        Dispatcher.UIThread.Post(() =>
        {
            IsConnected = false;
            Status = error is null ? "Sessão encerrada." : $"Sessão encerrada: {error}";
            CleanupSession();
        });
    }

    private void CleanupSession()
    {
        if (_session is not null)
        {
            _session.OutputReceived -= OnOutput;
            _session.Disconnected -= OnDisconnected;
            _session.Dispose();
            _session = null;
        }
    }
}
