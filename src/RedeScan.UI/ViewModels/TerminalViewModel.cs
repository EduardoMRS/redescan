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

    private readonly List<string> _history = new();
    private int _historyIndex;

    [RelayCommand]
    private async Task SendAsync()
    {
        if (_session is null || !IsConnected)
        {
            return;
        }

        // Envia o comando com quebra de linha (como pressionar Enter). O eco
        // aparece na tela porque o próprio servidor devolve o que recebe.
        var cmd = CommandInput ?? string.Empty;
        CommandInput = string.Empty;
        if (cmd.Length > 0 && (_history.Count == 0 || _history[^1] != cmd))
        {
            _history.Add(cmd);
        }

        _historyIndex = _history.Count;
        try
        {
            await _session.SendAsync(cmd + "\r");
        }
        catch (Exception ex)
        {
            Status = $"Falha ao enviar: {ex.Message}";
        }
    }

    /// <summary>Envia teclas especiais: "tab" (completar) e "ctrl-c" (interromper).</summary>
    [RelayCommand]
    private async Task SendKeyAsync(string? key)
    {
        if (_session is null || !IsConnected)
        {
            return;
        }

        var data = key switch
        {
            "tab" => CommandInput + "\t",
            "ctrl-c" => "\u0003",
            _ => null
        };
        if (data is null)
        {
            return;
        }

        if (key == "tab")
        {
            CommandInput = string.Empty;
        }

        try
        {
            await _session.SendAsync(data);
        }
        catch (Exception ex)
        {
            Status = $"Falha ao enviar: {ex.Message}";
        }
    }

    /// <summary>Seta para cima: comando anterior do histórico.</summary>
    public void HistoryPrevious()
    {
        if (_history.Count == 0)
        {
            return;
        }

        _historyIndex = Math.Max(0, _historyIndex - 1);
        CommandInput = _history[_historyIndex];
    }

    /// <summary>Seta para baixo: próximo comando do histórico.</summary>
    public void HistoryNext()
    {
        if (_history.Count == 0)
        {
            return;
        }

        _historyIndex = Math.Min(_history.Count, _historyIndex + 1);
        CommandInput = _historyIndex < _history.Count ? _history[_historyIndex] : string.Empty;
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
        var clean = AnsiFilter.Clean(text);
        Dispatcher.UIThread.Post(() =>
        {
            AnsiFilter.AppendWithBackspace(_buffer, clean);
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

/// <summary>
/// Limpa a saída do shell para exibição em texto simples: remove sequências
/// ANSI (cores, posicionamento, título da janela) e normaliza quebras de linha.
/// </summary>
public static partial class AnsiFilter
{
    public static string Clean(string text)
    {
        var s = AnsiRegex().Replace(text, string.Empty);
        s = s.Replace("\r\n", "\n");
        // \r sozinho (redesenho de linha) vira nada para não duplicar texto.
        s = s.Replace("\r", string.Empty);
        // Outros caracteres de controle, exceto \n, \t e backspace.
        return ControlRegex().Replace(s, string.Empty);
    }

    /// <summary>Aplica backspaces (\b) apagando o caractere anterior do buffer.</summary>
    public static void AppendWithBackspace(StringBuilder buffer, string text)
    {
        foreach (var ch in text)
        {
            if (ch == '\b')
            {
                if (buffer.Length > 0 && buffer[^1] != '\n')
                {
                    buffer.Length--;
                }
            }
            else
            {
                buffer.Append(ch);
            }
        }
    }

    // CSI (ESC [ ... letra), OSC (ESC ] ... BEL/ST) e escapes de 2 caracteres.
    [System.Text.RegularExpressions.GeneratedRegex(@"\x1B\[[0-?]*[ -/]*[@-~]|\x1B\][^\x07\x1B]*(\x07|\x1B\\)|\x1B[@-Z\\-_]")]
    private static partial System.Text.RegularExpressions.Regex AnsiRegex();

    [System.Text.RegularExpressions.GeneratedRegex(@"[\x00-\x07\x0B\x0C\x0E-\x1F\x7F]")]
    private static partial System.Text.RegularExpressions.Regex ControlRegex();
}
