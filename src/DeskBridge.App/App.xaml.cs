using System.Configuration;
using System.Data;
using System.Windows;
using DeskBridge.App.Services;

namespace DeskBridge.App;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    public ThemeService ThemeService { get; } = new();
    public LocalizationService LocalizationService { get; } = new();

    protected override void OnStartup(StartupEventArgs e)
    {
        LocalizationService.Apply("English");
        base.OnStartup(e);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        ThemeService.Dispose();
        base.OnExit(e);
    }
}

