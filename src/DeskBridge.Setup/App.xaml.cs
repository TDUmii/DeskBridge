using System.IO;
using System.Windows;
using System.Windows.Threading;

namespace DeskBridge.Setup;

public partial class App : Application
{
    public App()
    {
        DispatcherUnhandledException += OnDispatcherUnhandledException;
    }

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        if (e.Args.Contains("--quiet", StringComparer.OrdinalIgnoreCase))
        {
            ShutdownMode = ShutdownMode.OnExplicitShutdown;
            try
            {
                await new InstallerService().InstallAsync(new Progress<InstallProgress>(), CancellationToken.None);
                Shutdown(0);
            }
            catch (Exception exception)
            {
                WriteFailureLog(exception);
                Shutdown(1);
            }
            return;
        }

        var window = new MainWindow();
        MainWindow = window;
        window.Show();
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        WriteFailureLog(e.Exception);
        e.Handled = true;
        MessageBox.Show(
            "DeskBridge Setup could not continue. A short diagnostic was saved to %LOCALAPPDATA%\\DeskBridge\\setup-error.log.",
            "DeskBridge Setup",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
        Shutdown(1);
    }

    private static void WriteFailureLog(Exception exception)
    {
        try
        {
            string directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DeskBridge");
            Directory.CreateDirectory(directory);
            string entry = $"{DateTimeOffset.Now:O} | {exception.GetType().Name} | {exception.Message.ReplaceLineEndings(" ")}\r\n";
            File.AppendAllText(Path.Combine(directory, "setup-error.log"), entry);
        }
        catch
        {
            // Diagnostics must never hide the original setup failure.
        }
    }
}
