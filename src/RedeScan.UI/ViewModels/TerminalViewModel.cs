using System.Text;
using System.Threading;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RedeScan.Core.Services;

namespace RedeScan.UI.ViewModels;

/// <summary>Tipo de terminal de uma aba.</summary>
public enum TerminalKind
{
    Local,
    Ssh,
    Telnet
}

/// <summary>
/// Terminal embutido com três abas independentes: o shell deste dispositivo,
/// SSH (SSH.NET) e Telnet (cliente gerenciado). Cada aba tem sessão, saída e
/// histórico próprios — dá para manter o shell local aberto enquanto acessa
/// um roteador. Funciona em todas as plataformas (inclusive Android).
/// </summary>
public partial class TerminalViewModel : ViewModelBase
{
    public override string Title => "Terminal";
    public override string Icon => "⌨️";

    /// <summary>Mantém a chave antiga: preferências salvas (tela inicial/módulos) continuam valendo.</summary>
    public override string Key => "terminal";

    public TerminalTabViewModel LocalTab { get; }
    public TerminalTabViewModel SshTab { get; }
    public TerminalTabViewModel TelnetTab { get; }

    public IReadOnlyList<TerminalTabViewModel> Tabs { get; }

    [ObservableProperty] private TerminalTabViewModel _selectedTab;

    public TerminalViewModel(ISshTerminalFactory factory)
    {
        LocalTab = new TerminalTabViewModel(TerminalKind.Local, factory);
        SshTab = new TerminalTabViewModel(TerminalKind.Ssh, factory);
        TelnetTab = new TerminalTabViewModel(TerminalKind.Telnet, factory);
        Tabs = new[] { LocalTab, SshTab, TelnetTab };
        _selectedTab = SshTab;
    }

    /// <summary>Pré-preenche a aba SSH (usado ao abrir a partir do scanner).</summary>
    public void Prepare(string host, int port = 22, string? username = null) =>
        Prepare(TerminalKind.Ssh, host, port, username);

    /// <summary>Pré-preenche a aba do tipo informado e a seleciona.</summary>
    public void Prepare(TerminalKind kind, string host, int port, string? username = null)
    {
        var tab = kind switch
        {
            TerminalKind.Telnet => TelnetTab,
            TerminalKind.Local => LocalTab,
            _ => SshTab
        };
        tab.Prepare(host, port, username);
        SelectedTab = tab;
    }
}

/// <summary>Uma aba do terminal (uma sessão).</summary>
public partial class TerminalTabViewModel : ObservableObject
{
    private readonly ISshTerminalFactory _factory;
    private ITerminalSession? _session;
    private readonly StringBuilder _buffer = new();
    private readonly List<string> _history = new();
    private int _historyIndex;

    public TerminalTabViewModel(TerminalKind kind, ISshTerminalFactory factory)
    {
        Kind = kind;
        _factory = factory;
        _port = kind == TerminalKind.Telnet ? 23 : 22;
        _status = kind switch
        {
            TerminalKind.Local => "Abra o terminal deste dispositivo. Ele roda com o mesmo usuário do RedeScan.",
            TerminalKind.Telnet => "Informe host e porta. Usuário e senha são pedidos pelo próprio equipamento.",
            _ => "Informe host, usuário e senha para conectar via SSH."
        };
    }

    public TerminalKind Kind { get; }

    public string Header => Kind switch
    {
        TerminalKind.Local => "Este dispositivo",
        TerminalKind.Telnet => "Telnet",
        _ => "SSH"
    };

    public bool IsLocal => Kind == TerminalKind.Local;
    public bool IsRemote => Kind != TerminalKind.Local;
    public bool IsSsh => Kind == TerminalKind.Ssh;

    public string ConnectLabel => IsLocal ? "Abrir terminal" : "Conectar";
    public string DisconnectLabel => IsLocal ? "Encerrar" : "Desconectar";

    [ObservableProperty] private string _host = string.Empty;
    [ObservableProperty] private int _port;
    [ObservableProperty] private string _username = string.Empty;
    [ObservableProperty] private string _password = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsDisconnected))]
    private bool _isConnected;

    [ObservableProperty] private bool _isConnecting;
    [ObservableProperty] private string _output = string.Empty;
    [ObservableProperty] private string _commandInput = string.Empty;
    [ObservableProperty] private string _status;

    public bool IsDisconnected => !IsConnected;

    public void Prepare(string host, int port, string? username)
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
        if (IsConnected || IsConnecting)
        {
            return;
        }

        if (IsRemote && string.IsNullOrWhiteSpace(Host))
        {
            Status = "Informe o host.";
            return;
        }

        if (IsSsh && string.IsNullOrWhiteSpace(Username))
        {
            Status = "Informe o usuário.";
            return;
        }

        IsConnecting = true;
        try
        {
            switch (Kind)
            {
                case TerminalKind.Local:
                    var local = _factory.CreateLocal();
                    Attach(local);
                    Status = "Abrindo terminal local…";
                    await local.StartAsync();
                    Status = $"Terminal local: {local.ShellDescription}.";
                    break;

                case TerminalKind.Telnet:
                    var telnet = _factory.CreateTelnet();
                    Attach(telnet);
                    Status = $"Conectando via Telnet em {Host}:{Port}…";
                    await telnet.ConnectAsync(Host.Trim(), Port);
                    Status = $"Conectado (Telnet) a {Host}:{Port}. Atenção: Telnet não é criptografado.";
                    break;

                default:
                    var ssh = _factory.Create();
                    Attach(ssh);
                    Status = $"Conectando via SSH em {Host}:{Port}…";
                    await ssh.ConnectAsync(new SshConnectionInfo
                    {
                        Host = Host.Trim(),
                        Port = Port,
                        Username = Username.Trim(),
                        Password = Password
                    });
                    Status = $"Conectado (SSH) a {Host}:{Port}.";
                    break;
            }

            IsConnected = true;
        }
        catch (Exception ex)
        {
            Status = $"Falha ao conectar: {ex.Message}";
            CleanupSession();
        }
        finally
        {
            IsConnecting = false;
        }
    }

    private void Attach(ITerminalSession session)
    {
        _session = session;
        session.OutputReceived += OnOutput;
        session.Disconnected += OnDisconnected;
    }

    [RelayCommand]
    private async Task SendAsync()
    {
        if (_session is null || !IsConnected)
        {
            return;
        }

        var cmd = CommandInput ?? string.Empty;
        CommandInput = string.Empty;
        if (cmd.Length > 0 && (_history.Count == 0 || _history[^1] != cmd))
        {
            _history.Add(cmd);
        }

        _historyIndex = _history.Count;

        // Shell sem eco próprio (pipes, telnet sem WILL ECHO): mostra o comando.
        if (_session.NeedsLocalEcho)
        {
            AppendOutput(cmd + "\n");
        }

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
        Status = IsLocal ? "Terminal encerrado." : "Desconectado.";
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
        Dispatcher.UIThread.Post(() => AppendOutput(clean));
    }

    private void AppendOutput(string clean)
    {
        AnsiFilter.AppendWithBackspace(_buffer, clean);
        // Limita o histórico para não crescer indefinidamente.
        if (_buffer.Length > 200_000)
        {
            _buffer.Remove(0, _buffer.Length - 150_000);
        }

        Output = _buffer.ToString();
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
