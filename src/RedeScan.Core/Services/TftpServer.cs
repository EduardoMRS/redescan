using System.Net;
using System.Net.Sockets;
using System.Text;

namespace RedeScan.Core.Services;

/// <summary>
/// Implementação de <see cref="ITftpServer"/> seguindo a RFC 1350 (TFTP).
/// Cada transferência roda num socket UDP efêmero próprio (TID), como manda o
/// protocolo. Suporta leitura (RRQ) e, opcionalmente, escrita (WRQ).
/// </summary>
public sealed class TftpServer : ITftpServer
{
    private const int BlockSize = 512;

    // Opcodes TFTP.
    private const ushort OpRrq = 1;
    private const ushort OpWrq = 2;
    private const ushort OpData = 3;
    private const ushort OpAck = 4;
    private const ushort OpError = 5;

    private UdpClient? _listener;
    private CancellationTokenSource? _cts;
    private bool _allowWrite;

    public bool IsRunning { get; private set; }
    public string RootDirectory { get; private set; } = string.Empty;
    public int Port { get; private set; }

    public event Action<TftpLogEvent>? Log;

    public Task StartAsync(string rootDirectory, int port = 69, bool allowWrite = false, CancellationToken ct = default)
    {
        if (IsRunning)
        {
            return Task.CompletedTask;
        }

        RootDirectory = Path.GetFullPath(rootDirectory);
        Directory.CreateDirectory(RootDirectory);
        Port = port;
        _allowWrite = allowWrite;

        _listener = new UdpClient(new IPEndPoint(IPAddress.Any, port));
        _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        IsRunning = true;
        Emit($"Servidor TFTP ouvindo na porta {port} (raiz: {RootDirectory}). Escrita: {(allowWrite ? "permitida" : "bloqueada")}.");

        _ = Task.Run(() => AcceptLoopAsync(_cts.Token));
        return Task.CompletedTask;
    }

    public void Stop()
    {
        if (!IsRunning)
        {
            return;
        }

        _cts?.Cancel();
        _listener?.Dispose();
        _listener = null;
        IsRunning = false;
        Emit("Servidor TFTP parado.");
    }

    private async Task AcceptLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested && _listener is not null)
        {
            UdpReceiveResult req;
            try
            {
                req = await _listener.ReceiveAsync(ct);
            }
            catch (OperationCanceledException) { break; }
            catch (ObjectDisposedException) { break; }
            catch (Exception ex) { Emit($"Erro ao receber: {ex.Message}", true); continue; }

            // Cada requisição é tratada numa transferência isolada.
            var data = req.Buffer;
            var remote = req.RemoteEndPoint;
            _ = Task.Run(() => HandleRequestAsync(data, remote, ct));
        }
    }

    private async Task HandleRequestAsync(byte[] packet, IPEndPoint client, CancellationToken ct)
    {
        if (packet.Length < 4)
        {
            return;
        }

        var opcode = (ushort)((packet[0] << 8) | packet[1]);
        var (filename, mode) = ParseRequest(packet);

        // Novo socket efêmero (TID do servidor) para esta transferência.
        using var xfer = new UdpClient(new IPEndPoint(IPAddress.Any, 0));
        xfer.Connect(client);

        var safePath = ResolveSafePath(filename);
        if (safePath is null)
        {
            await SendErrorAsync(xfer, 2, "Acesso negado (caminho inválido).");
            Emit($"{client}: caminho recusado '{filename}'.", true);
            return;
        }

        try
        {
            if (opcode == OpRrq)
            {
                await ServeReadAsync(xfer, client, safePath, filename, ct);
            }
            else if (opcode == OpWrq)
            {
                if (!_allowWrite)
                {
                    await SendErrorAsync(xfer, 2, "Escrita desabilitada no servidor.");
                    Emit($"{client}: WRQ recusado (escrita desabilitada) para '{filename}'.", true);
                    return;
                }
                await ReceiveWriteAsync(xfer, client, safePath, filename, ct);
            }
            else
            {
                await SendErrorAsync(xfer, 4, "Operação não suportada.");
            }
        }
        catch (Exception ex)
        {
            Emit($"{client}: erro na transferência de '{filename}': {ex.Message}", true);
        }
    }

    // -------------------------------------------------------- RRQ (servir)
    private async Task ServeReadAsync(UdpClient xfer, IPEndPoint client, string path, string name, CancellationToken ct)
    {
        if (!File.Exists(path))
        {
            await SendErrorAsync(xfer, 1, "Arquivo não encontrado.");
            Emit($"{client}: RRQ '{name}' — arquivo não encontrado.", true);
            return;
        }

        Emit($"{client}: enviando '{name}'…");
        await using var fs = File.OpenRead(path);
        ushort block = 1;
        var buffer = new byte[BlockSize];
        int read;
        do
        {
            read = await fs.ReadAsync(buffer.AsMemory(0, BlockSize), ct);
            var dataPacket = BuildData(block, buffer, read);

            if (!await SendAndWaitAckAsync(xfer, dataPacket, block, ct))
            {
                Emit($"{client}: sem ACK do bloco {block}, abortando '{name}'.", true);
                return;
            }
            block++;
        }
        while (read == BlockSize && !ct.IsCancellationRequested);

        Emit($"{client}: '{name}' enviado com sucesso.");
    }

    // -------------------------------------------------------- WRQ (receber)
    private async Task ReceiveWriteAsync(UdpClient xfer, IPEndPoint client, string path, string name, CancellationToken ct)
    {
        Emit($"{client}: recebendo '{name}'…");
        await SendAckAsync(xfer, 0); // ACK do bloco 0 inicia a transferência

        await using var fs = File.Create(path);
        ushort expected = 1;
        while (!ct.IsCancellationRequested)
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(5000);

            UdpReceiveResult res;
            try { res = await xfer.ReceiveAsync(timeoutCts.Token); }
            catch (OperationCanceledException) { Emit($"{client}: timeout recebendo '{name}'.", true); return; }

            var pkt = res.Buffer;
            if (pkt.Length < 4) continue;
            var op = (ushort)((pkt[0] << 8) | pkt[1]);
            var blk = (ushort)((pkt[2] << 8) | pkt[3]);
            if (op != OpData) continue;

            if (blk == expected)
            {
                await fs.WriteAsync(pkt.AsMemory(4), ct);
                await SendAckAsync(xfer, blk);
                expected++;
                if (pkt.Length - 4 < BlockSize)
                {
                    Emit($"{client}: '{name}' recebido ({fs.Length} bytes).");
                    return;
                }
            }
            else
            {
                // Reenvia o ACK do último bloco aceito (duplicata).
                await SendAckAsync(xfer, (ushort)(expected - 1));
            }
        }
    }

    // -------------------------------------------------------------- helpers
    private static (string Filename, string Mode) ParseRequest(byte[] packet)
    {
        // Formato: opcode(2) | filename\0 | mode\0
        var idx = 2;
        var filename = ReadZ(packet, ref idx);
        var mode = ReadZ(packet, ref idx);
        return (filename, mode);
    }

    private static string ReadZ(byte[] b, ref int idx)
    {
        var start = idx;
        while (idx < b.Length && b[idx] != 0) idx++;
        var s = Encoding.ASCII.GetString(b, start, idx - start);
        idx++; // pula o \0
        return s;
    }

    /// <summary>Resolve o caminho dentro da raiz, bloqueando directory traversal.</summary>
    private string? ResolveSafePath(string filename)
    {
        if (string.IsNullOrWhiteSpace(filename))
        {
            return null;
        }

        // Usa só o nome do arquivo (ignora diretórios enviados pelo cliente).
        var name = Path.GetFileName(filename);
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        var full = Path.GetFullPath(Path.Combine(RootDirectory, name));
        return full.StartsWith(RootDirectory, StringComparison.Ordinal) ? full : null;
    }

    private static byte[] BuildData(ushort block, byte[] data, int length)
    {
        var p = new byte[4 + length];
        p[0] = OpData >> 8; p[1] = OpData & 0xFF;
        p[2] = (byte)(block >> 8); p[3] = (byte)(block & 0xFF);
        Array.Copy(data, 0, p, 4, length);
        return p;
    }

    private static async Task<bool> SendAndWaitAckAsync(UdpClient xfer, byte[] dataPacket, ushort block, CancellationToken ct)
    {
        // Até 4 tentativas com timeout de 2s (retransmissão simples).
        for (var attempt = 0; attempt < 4; attempt++)
        {
            await xfer.SendAsync(dataPacket, dataPacket.Length);

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(2000);
            try
            {
                var res = await xfer.ReceiveAsync(timeoutCts.Token);
                var pkt = res.Buffer;
                if (pkt.Length >= 4)
                {
                    var op = (ushort)((pkt[0] << 8) | pkt[1]);
                    var blk = (ushort)((pkt[2] << 8) | pkt[3]);
                    if (op == OpAck && blk == block)
                    {
                        return true;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                if (ct.IsCancellationRequested) return false;
                // timeout → retransmite
            }
        }
        return false;
    }

    private static async Task SendAckAsync(UdpClient xfer, ushort block)
    {
        var p = new byte[4];
        p[0] = OpAck >> 8; p[1] = OpAck & 0xFF;
        p[2] = (byte)(block >> 8); p[3] = (byte)(block & 0xFF);
        await xfer.SendAsync(p, p.Length);
    }

    private static async Task SendErrorAsync(UdpClient xfer, ushort code, string message)
    {
        var msg = Encoding.ASCII.GetBytes(message);
        var p = new byte[4 + msg.Length + 1];
        p[0] = OpError >> 8; p[1] = OpError & 0xFF;
        p[2] = (byte)(code >> 8); p[3] = (byte)(code & 0xFF);
        Array.Copy(msg, 0, p, 4, msg.Length);
        await xfer.SendAsync(p, p.Length);
    }

    private void Emit(string message, bool isError = false)
        => Log?.Invoke(new TftpLogEvent(DateTime.Now, message, isError));

    public void Dispose()
    {
        Stop();
        _cts?.Dispose();
    }
}
