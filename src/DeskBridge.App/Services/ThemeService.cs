using System.Windows;
using System.Windows.Media;
using System.Windows.Interop;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace DeskBridge.App.Services;

public sealed class ThemeService : IDisposable
{
    private string _mode = "system";

    public ThemeService() => SystemEvents.UserPreferenceChanged += SystemPreferenceChanged;

    public void Apply(string? mode)
    {
        _mode = mode?.ToLowerInvariant() is "light" or "dark" ? mode.ToLowerInvariant() : "system";
        var dark = _mode == "dark" || (_mode == "system" && SystemUsesDarkMode());
        foreach (var (key, color) in dark ? ThemePalette.Dark : ThemePalette.Light)
            Application.Current.Resources[key] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));
        foreach (Window window in Application.Current.Windows) ApplyNativeTitleBar(window, dark);
    }

    private void SystemPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
    {
        if (_mode == "system") Application.Current.Dispatcher.Invoke(() => Apply(_mode));
    }

    private static bool SystemUsesDarkMode()
    {
        using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
        return key?.GetValue("AppsUseLightTheme") is int value && value == 0;
    }

    public void Dispose() => SystemEvents.UserPreferenceChanged -= SystemPreferenceChanged;

    public static void ApplyNativeTitleBar(Window window, bool dark)
    {
        var handle = new WindowInteropHelper(window).Handle;
        if (handle == IntPtr.Zero) return;
        var enabled = dark ? 1 : 0;
        _ = DwmSetWindowAttribute(handle, 20, ref enabled, sizeof(int));
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr window, int attribute, ref int value, int valueSize);
}

internal static class ThemePalette
{
    public static IReadOnlyDictionary<string, string> Light { get; } = new Dictionary<string, string>
    {
        ["CanvasBrush"] = "#F4F2ED", ["SurfaceBrush"] = "#FFFFFF", ["InkBrush"] = "#17212A",
        ["MutedBrush"] = "#64717B", ["LineBrush"] = "#D9D5CC", ["ControlBrush"] = "#FFFFFF",
        ["ControlHoverBrush"] = "#EEF2F5", ["ControlBorderBrush"] = "#AEB7BF", ["HeaderBrush"] = "#17212A",
        ["HeaderInkBrush"] = "#FFFFFF", ["HeaderMutedBrush"] = "#BAC5CD", ["PrimaryBrush"] = "#1262A3",
        ["SuccessBrush"] = "#14785C", ["SuccessSurfaceBrush"] = "#DCEFE8", ["DangerBrush"] = "#8A2926",
        ["DangerSurfaceBrush"] = "#F8DEDC", ["WarningBrush"] = "#6F5111", ["WarningSurfaceBrush"] = "#F5E9CA"
    };

    public static IReadOnlyDictionary<string, string> Dark { get; } = new Dictionary<string, string>
    {
        ["CanvasBrush"] = "#101418", ["SurfaceBrush"] = "#181E23", ["InkBrush"] = "#EDF2F5",
        ["MutedBrush"] = "#A8B3BC", ["LineBrush"] = "#303940", ["ControlBrush"] = "#20272D",
        ["ControlHoverBrush"] = "#29333B", ["ControlBorderBrush"] = "#56636D", ["HeaderBrush"] = "#0B0F12",
        ["HeaderInkBrush"] = "#F7FAFC", ["HeaderMutedBrush"] = "#A9B7C1", ["PrimaryBrush"] = "#4C9FE3",
        ["SuccessBrush"] = "#66D1AA", ["SuccessSurfaceBrush"] = "#173B30", ["DangerBrush"] = "#F09A94",
        ["DangerSurfaceBrush"] = "#442523", ["WarningBrush"] = "#E8C56D", ["WarningSurfaceBrush"] = "#3C321B"
    };
}
