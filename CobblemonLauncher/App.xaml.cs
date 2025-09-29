using System;
using System.IO;
using System.Windows;

namespace CobblemonLauncher;

public partial class App : Application
{
    private static readonly string LogPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "app.log");

    protected override void OnStartup(StartupEventArgs e)
    {
        File.AppendAllText(LogPath, $"[{DateTime.Now}] App.OnStartup starting\n");

        AppDomain.CurrentDomain.UnhandledException += (s, args) =>
        {
            try
            {
                File.AppendAllText(
                    LogPath,
                    $"[{DateTime.Now}] AppDomain Exception: {args.ExceptionObject}\n"
                );
            }
            catch { }
        };

        DispatcherUnhandledException += (s, args) =>
        {
            try
            {
                File.AppendAllText(
                    LogPath,
                    $"[{DateTime.Now}] Dispatcher Exception: {args.Exception}\n"
                );
                args.Handled = false;
            }
            catch { }
        };

        base.OnStartup(e);
        File.AppendAllText(LogPath, $"[{DateTime.Now}] base.OnStartup completed\n");
    }

    protected override void OnExit(ExitEventArgs e)
    {
        File.AppendAllText(LogPath, $"[{DateTime.Now}] App.OnExit called with code: {e.ApplicationExitCode}\n");
        base.OnExit(e);
    }
}
