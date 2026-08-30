using System.Windows;

namespace DeskBridge.Setup;

public partial class App : Application
{
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
            catch
            {
                Shutdown(1);
            }
            return;
        }

        var window = new MainWindow();
        MainWindow = window;
        window.Show();
    }
}
