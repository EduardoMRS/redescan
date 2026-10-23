using System.Net.Sockets;
using System.Text;

namespace RedeScan.Core.Services;

/// <summary>
/// Cliente Telnet (RFC 854) 100% gerenciado. Negocia o mínimo para CLI de
/// equipamentos de rede: aceita ECHO e SUPPRESS-GO-AHEAD do servidor e recusa
/// as demais opções. O login (usuário/senha) acontece no próprio fluxo, como
/// num cliente telnet comum.
/// </summary>
public sealed class TelnetTerminalSession : ITelnetTerminalSession
{
    // Comandos e opções Telnet.
    internal const byte Iac = 255, Dont = 254, Do = 253, Wont = 252, Will = 251, Sb = 250, Se = 240;
    internal const byte OptEcho = 1, OptSuppressGoAhead = 3;

    private TcpClient? _client;
    private NetworkStream? _stream;
    private CancellationTokenSource? _readCts;
    private readonly SemaphoreSlim _writeGate = new(1, 1);
    private bool _serverEchoes;

    public event Action<string>? OutputReceived;
    public event Action<string?>? Disconnected;

    public bool IsConnected => _client?.Connected == true;

    /// <summary>Sem o servidor ecoar (WILL ECHO), a UI mostra o que foi digitado.</summary>
    public bool NeedsLocalEcho => !_serverEchoes;

    public async Task ConnectAsync(string host, int port = 23, int timeoutSeconds = 15, CancellationToken ct = default)
    {
        _client = new TcpClient();
        using (var connectCts = CancellationTokenSource.CreateLinkedTokenSource(ct))
        {
            connectCts.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));
            try
            {
                await _client.ConnectAsync(host, port, connectCts.Token);
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                _client.Dispose();
                _client = null;
                throw new TimeoutException($"Tempo esgotado ao conectar em {host}:{port}.");
            }
        }

        _stream = _client.GetStream();
        _readCts = new CancellationTokenSource();
        _ = Task.Run(() => ReadLoopAsync(_readCts.Token));
    }

    private async Task ReadLoopAsync(CancellationToken ct)
    {
        var buffer = new byte[4096];
        var parser = new TelnetParser();
        var decoder = Encoding.UTF8.GetDecoder();
        try
        {
            while (!ct.IsCancellationRequested && _stream is not null)
            {
                var read = await _stream.ReadAsync(buffer, ct);
                if (read == 0)
                {
                    break; // servidor fechou
                }

                var (data, replies) = parser.Feed(buffer.AsSpan(0, read));
                if (parser.ServerWillEcho is { } echo)
                {
                    _serverEchoes = echo;
                }

                if (replies.Length > 0)
                {
                    await WriteRawAsync(replies, ct);
                }

                if (data.Length > 0)
                {
                    var chars = new char[decoder.GetCharCount(data, 0, data.Length)];
                    decoder.GetChars(data, 0, data.Length, chars, 0);
                    OutputReceived?.Invoke(new string(chars));
                }
            }
        }
        catch (OperationCanceledException)
        {
            // encerramento normal
        }
        catch (Exception ex) when (ex is IOException or SocketException or ObjectDisposedException)
        {
            if (!ct.IsCancellationRequested)
            {
                Disconnected?.Invoke(ex.Message);
                return;
            }
        }

        Disconnected?.Invoke(null);
    }

    public Task SendAsync(string data, CancellationToken ct = default)
    {
        // Telnet usa CR LF como fim de linha (RFC 854).
        var text = data.Replace("\r\n", "\r").Replace("\r", "\r\n");
        return WriteRawAsync(TelnetParser.EscapeIac(Encoding.UTF8.GetBytes(text)), ct);
    }

    private async Task WriteRawAsync(byte[] bytes, CancellationToken ct)
    {
        if (_stream is null)
        {
            return;
        }

        await _writeGate.WaitAsync(ct);
        try
        {
            await _stream.WriteAsync(bytes, ct);
            await _stream.FlushAsync(ct);
        }
        finally
        {
            _writeGate.Release();
        }
    }

    public void Disconnect()
    {
        _readCts?.Cancel();
        try { _stream?.Dispose(); } catch { /* ignore */ }
        try { _client?.Close(); } catch { /* ignore */ }
        _stream = null;
    }

    public void Dispose()
    {
        Disconnect();
        _readCts?.Dispose();
        _client?.Dispose();
        _client = null;
    }
}

/// <summary>
/// Separa dados de comandos Telnet e monta as respostas de negociação.
/// Mantém estado entre chamadas (um comando pode chegar partido em dois pacotes).
/// </summary>
internal sealed class TelnetParser
{
    private enum State { Data, Iac, Option, Sub, SubIac }

    private State _state = State.Data;
    private byte _verb;

    /// <summary>Última decisão sobre o servidor ecoar (null = ainda não negociado).</summary>
    public bool? ServerWillEcho { get; private set; }

    public (byte[] Data, byte[] Replies) Feed(ReadOnlySpan<byte> input)
    {
        var data = new List<byte>(input.Length);
        var replies = new List<byte>();
        foreach (var b in input)
        {
            switch (_state)
            {
                case State.Data:
                    if (b == TelnetTerminalSession.Iac)
                    {
                        _state = State.Iac;
                    }
                    else
                    {
                        data.Add(b);
                    }

                    break;

                case State.Iac:
                    switch (b)
                    {
                        case TelnetTerminalSession.Iac:
                            data.Add(255); // IAC IAC = byte 255 literal
                            _state = State.Data;
                            break;
                        case TelnetTerminalSession.Do or TelnetTerminalSession.Dont
                            or TelnetTerminalSession.Will or TelnetTerminalSession.Wont:
                            _verb = b;
                            _state = State.Option;
                            break;
                        case TelnetTerminalSession.Sb:
                            _state = State.Sub;
                            break;
                        default:
                            _state = State.Data; // GA, NOP etc.: ignora
                            break;
                    }

                    break;

                case State.Option:
                    Negotiate(_verb, b, replies);
                    _state = State.Data;
                    break;

                case State.Sub:
                    if (b == TelnetTerminalSession.Iac)
                    {
                        _state = State.SubIac;
                    }

                    break;

                case State.SubIac:
                    _state = b == TelnetTerminalSession.Se ? State.Data : State.Sub;
                    break;
            }
        }

        return (data.ToArray(), replies.ToArray());
    }

    private void Negotiate(byte verb, byte option, List<byte> replies)
    {
        const byte iac = TelnetTerminalSession.Iac;
        var accepted = option is TelnetTerminalSession.OptEcho or TelnetTerminalSession.OptSuppressGoAhead;
        switch (verb)
        {
            case TelnetTerminalSession.Will:
                // Servidor quer ativar algo do lado dele: aceita ECHO/SGA.
                replies.AddRange(new[] { iac, accepted ? TelnetTerminalSession.Do : TelnetTerminalSession.Dont, option });
                if (option == TelnetTerminalSession.OptEcho)
                {
                    ServerWillEcho = true;
                }

                break;
            case TelnetTerminalSession.Wont:
                replies.AddRange(new[] { iac, TelnetTerminalSession.Dont, option });
                if (option == TelnetTerminalSession.OptEcho)
                {
                    ServerWillEcho = false;
                }

                break;
            case TelnetTerminalSession.Do:
                // Servidor pede que NÓS ativemos algo: só SGA.
                var weDo = option == TelnetTerminalSession.OptSuppressGoAhead;
                replies.AddRange(new[] { iac, weDo ? TelnetTerminalSession.Will : TelnetTerminalSession.Wont, option });
                break;
            case TelnetTerminalSession.Dont:
                replies.AddRange(new[] { iac, TelnetTerminalSession.Wont, option });
                break;
        }
    }

    /// <summary>Duplica bytes 255 nos dados enviados (IAC literal).</summary>
    public static byte[] EscapeIac(byte[] data)
    {
        if (Array.IndexOf(data, (byte)255) < 0)
        {
            return data;
        }

        var list = new List<byte>(data.Length + 4);
        foreach (var b in data)
        {
            list.Add(b);
            if (b == 255)
            {
                list.Add(255);
            }
        }

        return list.ToArray();
    }
}
