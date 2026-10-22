using System.Text;
using Renci.SshNet;
using Renci.SshNet.Common;

namespace RedeScan.Core.Services;

/// <summary>
/// Implementação de <see cref="ISshTerminalSession"/> sobre o SSH.NET
/// (<see cref="ShellStream"/>). Lê a saída em background e a entrega via evento.
/// </summary>
public sealed class SshTerminalSession : ISshTerminalSession
{
    private SshClient? _client;
    private ShellStream? _shell;
    private CancellationTokenSource? _readCts;

    public event Action<string>? OutputReceived;
    public event Action<string?>? Disconnected;

    public bool IsConnected => _client?.IsConnected == true;

    public async Task ConnectAsync(SshConnectionInfo info, CancellationToken ct = default)
    {
        var connInfo = new ConnectionInfo(
            info.Host,
            info.Port,
            info.Username,
            new PasswordAuthenticationMethod(info.Username, info.Password))
        {
            Timeout = TimeSpan.FromSeconds(info.TimeoutSeconds)
        };

        _client = new SshClient(connInfo);
        await Task.Run(() => _client.Connect(), ct);

        // Terminal pseudo (80x24) suficiente para CLI.
        _shell = _client.CreateShellStream("xterm", 80, 24, 800, 600, 4096);

        _readCts = new CancellationTokenSource();
        _ = Task.Run(() => ReadLoopAsync(_readCts.Token));
    }

    private async Task ReadLoopAsync(CancellationToken ct)
    {
        var buffer = new byte[4096];
        try
        {
            while (!ct.IsCancellationRequested && _shell is not null)
            {
                var read = await _shell.ReadAsync(buffer, 0, buffer.Length, ct);
                if (read > 0)
                {
                    OutputReceived?.Invoke(Encoding.UTF8.GetString(buffer, 0, read));
                }
                else
                {
                    await Task.Delay(30, ct);
                }

                if (_client is { IsConnected: false })
                {
                    break;
                }
            }
        }
        catch (OperationCanceledException)
        {
            // encerramento normal
        }
        catch (Exception ex)
        {
            Disconnected?.Invoke(ex.Message);
            return;
        }

        Disconnected?.Invoke(null);
    }

    public Task SendAsync(string data, CancellationToken ct = default)
    {
        if (_shell is null)
        {
            return Task.CompletedTask;
        }

        var bytes = Encoding.UTF8.GetBytes(data);
        return _shell.WriteAsync(bytes, 0, bytes.Length, ct);
    }

    public void Disconnect()
    {
        _readCts?.Cancel();
        try { _shell?.Dispose(); } catch { /* ignore */ }
        try { if (_client is { IsConnected: true }) _client.Disconnect(); } catch { /* ignore */ }
        _shell = null;
    }

    public void Dispose()
    {
        Disconnect();
        _readCts?.Dispose();
        _client?.Dispose();
        _client = null;
    }
}

/// <summary>Fábrica padrão de <see cref="SshTerminalSession"/>.</summary>
public sealed class SshTerminalFactory : ISshTerminalFactory
{
    public ISshTerminalSession Create() => new SshTerminalSession();
}
