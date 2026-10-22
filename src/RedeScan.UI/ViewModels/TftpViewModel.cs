using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RedeScan.Core.Services;

namespace RedeScan.UI.ViewModels;

/// <summary>
/// Servidor TFTP embutido para recuperação de firmware. Serve arquivos de uma
/// pasta (e opcionalmente recebe uploads) para dispositivos em modo de
/// recuperação. A porta padrão do TFTP (69) exige privilégio; uma porta alta
/// (ex.: 6969) roda sem admin/root.
/// </summary>
public partial class TftpViewModel : ViewModelBase
{
    private readonly ITftpServer _tftp;

    public override string Title => "TFTP (Firmware)";
    public override string Icon => "📤";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsStopped))]
    private bool _isRunning;

    [ObservableProperty] private string _rootDirectory = string.Empty;
    [ObservableProperty] private int _port = 6969;
    [ObservableProperty] private bool _allowWrite;
    [ObservableProperty] private string _status =
        "Escolha a pasta com o firmware e inicie o servidor. Porta 69 exige admin/root; use 6969 para rodar sem privilégio.";

    public bool IsStopped => !IsRunning;

    public ObservableCollection<string> LogLines { get; } = new();

    public TftpViewModel(ITftpServer tftp)
    {
        _tftp = tftp;
        _tftp.Log += OnLog;

        // Pasta padrão: ~/RedeScan-TFTP
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        RootDirectory = System.IO.Path.Combine(home, "RedeScan-TFTP");
    }

    [RelayCommand]
    private async Task StartAsync()
    {
        if (IsRunning)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(RootDirectory))
        {
            Status = "Informe a pasta raiz do TFTP.";
            return;
        }

        try
        {
            await _tftp.StartAsync(RootDirectory, Port, AllowWrite);
            IsRunning = _tftp.IsRunning;
        }
        catch (Exception ex)
        {
            // Erro típico: porta 69 sem privilégio (Permission denied / Access denied).
            Status = Port < 1024
                ? $"Falha ao abrir a porta {Port}: {ex.Message}. Porta < 1024 exige admin/root — tente 6969."
                : $"Falha ao iniciar o TFTP: {ex.Message}";
        }
    }

    [RelayCommand]
    private void Stop()
    {
        _tftp.Stop();
        IsRunning = _tftp.IsRunning;
    }

    [RelayCommand]
    private void ClearLog() => LogLines.Clear();

    private void OnLog(TftpLogEvent e)
    {
        // Evento vem de thread de background: marshala para a UI.
        Dispatcher.UIThread.Post(() =>
        {
            var prefix = e.IsError ? "✗" : "•";
            LogLines.Insert(0, $"{e.Timestamp:HH:mm:ss} {prefix} {e.Message}");
            if (LogLines.Count > 500)
            {
                LogLines.RemoveAt(LogLines.Count - 1);
            }
            Status = e.Message;
        });
    }
}
