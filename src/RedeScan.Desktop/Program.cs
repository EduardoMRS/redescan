using System.Runtime.InteropServices;
using Avalonia;
using RedeScan.Core.Diagnostics;
using RedeScan.UI;

namespace RedeScan.Desktop;

internal static class Program
{
    // Ponto de entrada. Não use APIs do Avalonia antes de AppMain ser chamado:
    // tudo que depende de SynchronizationContext inicializado deve ficar depois.
    [STAThread]
    public static int Main(string[] args)
    {
        AttachParentConsoleOnWindows();
        AppLog.MinimumLevel = args.Contains("--verbose") ? LogLevel.Debug : LogLevel.Info;
        AppLog.InstallGlobalHandlers();
        AppLog.Info($"RedeScan {typeof(Program).Assembly.GetName().Version} iniciando — " +
                    $"{RuntimeInformation.OSDescription}, usuário {Environment.UserName}. Log: {AppLog.LogFile}");

        try
        {
            App.Services = Composition.BuildServiceProvider();
            return BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
        catch (Exception ex)
        {
            var report = AppLog.WriteCrash("Program.Main", ex);
            Console.Error.WriteLine("[RedeScan] O aplicativo encerrou por um erro. " +
                                    (report is null ? string.Empty : $"Detalhes em {report}"));
            return 1;
        }
        finally
        {
            AppLog.Info("RedeScan encerrado.");
        }
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();

    /// <summary>
    /// No Windows o executável é WinExe (sem console próprio). Quando iniciado
    /// por um terminal (cmd/PowerShell), anexa ao console dele para que o log
    /// apareça na tela, como no Linux.
    /// </summary>
    private static void AttachParentConsoleOnWindows()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        try
        {
            if (AttachConsole(AttachParentProcess))
            {
                var stderr = new StreamWriter(Console.OpenStandardError()) { AutoFlush = true };
                Console.SetError(stderr);
                var stdout = new StreamWriter(Console.OpenStandardOutput()) { AutoFlush = true };
                Console.SetOut(stdout);
            }
        }
        catch
        {
            // sem console pai — segue só com o arquivo de log
        }
    }

    private const int AttachParentProcess = -1;

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool AttachConsole(int processId);
}
