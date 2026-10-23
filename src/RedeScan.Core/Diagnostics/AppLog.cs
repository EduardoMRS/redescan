using System.Diagnostics;
using System.Text;

namespace RedeScan.Core.Diagnostics;

/// <summary>Nível de uma entrada de log.</summary>
public enum LogLevel
{
    Debug,
    Info,
    Warning,
    Error,
    Fatal
}

/// <summary>
/// Log simples e thread-safe do RedeScan. Grava em <c>logs/redescan.log</c>
/// (rotação por tamanho) e espelha no console (stderr) — assim, ao iniciar pelo
/// terminal (ex.: <c>sudo redescan</c>), o usuário vê o que está acontecendo.
/// Crashes geram também um arquivo próprio <c>crash-AAAAMMDD-HHMMSS.log</c>.
/// </summary>
public static class AppLog
{
    private const long MaxLogBytes = 5 * 1024 * 1024;
    private static readonly object Gate = new();
    private static string? _logFile;

    /// <summary>Espelha as entradas no console. Ligado por padrão.</summary>
    public static bool MirrorToConsole { get; set; } = true;

    /// <summary>Nível mínimo registrado.</summary>
    public static LogLevel MinimumLevel { get; set; } = LogLevel.Info;

    /// <summary>Caminho do arquivo de log atual.</summary>
    /// <remarks>
    /// O nome inclui o usuário: a pasta de logs da instalação (/opt/redescan/logs)
    /// é compartilhada, e cada usuário precisa do próprio arquivo.
    /// </remarks>
    public static string LogFile => _logFile ??= Path.Combine(AppPaths.LogsDir, $"redescan-{SafeUser}.log");

    private static string SafeUser =>
        string.Concat(Environment.UserName.Select(c => char.IsLetterOrDigit(c) || c is '-' or '_' ? c : '_'));

    public static void Debug(string message) => Write(LogLevel.Debug, message);
    public static void Info(string message) => Write(LogLevel.Info, message);
    public static void Warn(string message) => Write(LogLevel.Warning, message);

    public static void Error(string message, Exception? ex = null) =>
        Write(LogLevel.Error, ex is null ? message : $"{message}{Environment.NewLine}{ex}");

    /// <summary>Registra uma entrada no log.</summary>
    public static void Write(LogLevel level, string message)
    {
        if (level < MinimumLevel)
        {
            return;
        }

        var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{LevelTag(level)}] {message}";

        lock (Gate)
        {
            if (MirrorToConsole)
            {
                try
                {
                    Console.Error.WriteLine(line);
                }
                catch
                {
                    // sem console — ignora
                }
            }

            try
            {
                RotateIfNeeded();
                File.AppendAllText(LogFile, line + Environment.NewLine, Encoding.UTF8);
            }
            catch
            {
                // log nunca pode derrubar o app
            }
        }
    }

    /// <summary>
    /// Registra um crash: grava um arquivo dedicado com o contexto do sistema e
    /// a exceção completa, e devolve o caminho dele.
    /// </summary>
    public static string? WriteCrash(string source, Exception? ex)
    {
        var report = new StringBuilder()
            .AppendLine("RedeScan — relatório de falha")
            .AppendLine($"Data:       {DateTime.Now:yyyy-MM-dd HH:mm:ss}")
            .AppendLine($"Origem:     {source}")
            .AppendLine($"Versão:     {typeof(AppLog).Assembly.GetName().Version}")
            .AppendLine($"SO:         {System.Runtime.InteropServices.RuntimeInformation.OSDescription}")
            .AppendLine($".NET:       {Environment.Version}")
            .AppendLine($"Processo:   {Environment.ProcessPath}")
            .AppendLine($"Usuário:    {Environment.UserName}")
            .AppendLine()
            .AppendLine(ex?.ToString() ?? "(sem exceção)")
            .ToString();

        Write(LogLevel.Fatal, $"Falha não tratada ({source}): {ex}");

        try
        {
            var path = Path.Combine(AppPaths.LogsDir, $"crash-{SafeUser}-{DateTime.Now:yyyyMMdd-HHmmss}.log");
            File.WriteAllText(path, report, Encoding.UTF8);
            try
            {
                Console.Error.WriteLine($"[RedeScan] Relatório de falha salvo em: {path}");
            }
            catch
            {
                // sem console
            }

            return path;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Instala os handlers globais de exceção (AppDomain e Tasks). Deve ser
    /// chamado o mais cedo possível no <c>Main</c>.
    /// </summary>
    public static void InstallGlobalHandlers()
    {
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            WriteCrash("AppDomain.UnhandledException", e.ExceptionObject as Exception);

        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            Write(LogLevel.Error, $"Task com exceção não observada: {e.Exception}");
            e.SetObserved();
        };

        // Encaminha mensagens de Trace (ex.: avisos do Avalonia) para o log.
        Trace.Listeners.Add(new AppLogTraceListener());
    }

    private static void RotateIfNeeded()
    {
        var file = new FileInfo(LogFile);
        if (file.Exists && file.Length > MaxLogBytes)
        {
            var old = LogFile + ".1";
            File.Delete(old);
            File.Move(LogFile, old);
        }
    }

    private static string LevelTag(LogLevel level) => level switch
    {
        LogLevel.Debug => "DBG",
        LogLevel.Info => "INF",
        LogLevel.Warning => "WRN",
        LogLevel.Error => "ERR",
        _ => "FTL"
    };

    /// <summary>Redireciona Trace/Debug para o <see cref="AppLog"/>.</summary>
    private sealed class AppLogTraceListener : TraceListener
    {
        public override void Write(string? message)
        {
        }

        public override void WriteLine(string? message)
        {
            if (!string.IsNullOrWhiteSpace(message))
            {
                AppLog.Write(LogLevel.Warning, "[trace] " + message);
            }
        }
    }
}
