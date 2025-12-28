using System.Windows;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace Kresti4kHelper;

public partial class App : Application
{
    private const string CrashLogFileName = "crash.log";

    protected override void OnStartup(StartupEventArgs e)
    {
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

        base.OnStartup(e);
    }

    private static void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        WriteCrashLog("AppDomain.CurrentDomain.UnhandledException", e.ExceptionObject as Exception, e.IsTerminating);
    }

    private static void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        WriteCrashLog("Application.DispatcherUnhandledException", e.Exception, e.Handled);
    }

    private static void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        WriteCrashLog("TaskScheduler.UnobservedTaskException", e.Exception, e.Observed);
    }

    private static void WriteCrashLog(string source, Exception? exception, bool flag)
    {
        try
        {
            var basePath = AppDomain.CurrentDomain.BaseDirectory;
            var path = Path.Combine(basePath, CrashLogFileName);
            var timestamp = DateTimeOffset.Now.ToString("O");
            var contents = $"[{timestamp}] {source} (Flag: {flag}){Environment.NewLine}{exception?.ToString() ?? "No exception details."}{Environment.NewLine}{Environment.NewLine}";

            File.AppendAllText(path, contents);
        }
        catch
        {

        }
    }
}