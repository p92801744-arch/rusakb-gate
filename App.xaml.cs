using System.IO;
using System.Windows;
using System.Windows.Threading;
using RusakbGate.Services;

namespace RusakbGate;

public partial class App : Application
{
    private Mutex? _single;

    protected override void OnStartup(StartupEventArgs e)
    {
        _single = new Mutex(true, @"Local\RusakbGate.SingleInstance", out var created);
        if (!created)
        {
            MessageBox.Show(
                "RusakbGate уже запущен.\nПосмотри значок в панели задач.",
                "RusakbGate",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            Shutdown();
            return;
        }

        DispatcherUnhandledException += OnDispatcherUnhandled;
        AppDomain.CurrentDomain.UnhandledException += OnDomainUnhandled;
        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            try { ProfileStore.AppendLog("UNOBSERVED " + args.Exception); } catch { /* ignore */ }
            args.SetObserved();
        };

        base.OnStartup(e);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        try { _single?.ReleaseMutex(); } catch { /* ignore */ }
        _single?.Dispose();
        base.OnExit(e);
    }

    private static void OnDispatcherUnhandled(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        Report(e.Exception);
        e.Handled = true;
    }

    private static void OnDomainUnhandled(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
            Report(ex);
    }

    private static void Report(Exception ex)
    {
        try
        {
            Directory.CreateDirectory(ProfileStore.AppDir);
            File.AppendAllText(ProfileStore.LogPath,
                DateTime.Now.ToString("s") + " CRASH " + ex + Environment.NewLine);
        }
        catch { /* ignore */ }

        try
        {
            MessageBox.Show(
                ex.Message + "\n\nПодробности: " + ProfileStore.LogPath,
                "RusakbGate — ошибка",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        catch { /* ignore */ }
    }
}
