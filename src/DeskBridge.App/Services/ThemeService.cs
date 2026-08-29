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
        ["CanvasBrush"] = "#F3F6FA", ["SurfaceBrush"] = "#FFFFFF", ["PanelRaisedBrush"] = "#F8FAFD",
        ["FieldBrush"] = "#FFFFFF", ["InkBrush"] = "#142033", ["MutedBrush"] = "#5B687B",
        ["LineBrush"] = "#D8E0EA", ["ControlBrush"] = "#FFFFFF", ["ControlHoverBrush"] = "#EAF1F8",
        ["ControlBorderBrush"] = "#AEBBCB", ["HeaderBrush"] = "#101722", ["HeaderInkBrush"] = "#F7FAFF",
        ["HeaderMutedBrush"] = "#AEBBCB", ["PrimaryBrush"] = "#147DCC", ["PrimaryHoverBrush"] = "#0B68B0",
        ["AccentSoftBrush"] = "#DDF3FF", ["FocusBrush"] = "#4CC9F0", ["SuccessBrush"] = "#07866F",
        ["SuccessSurfaceBrush"] = "#D9F4EC", ["DangerBrush"] = "#A33A43", ["DangerSurfaceBrush"] = "#FCE5E7",
        ["WarningBrush"] = "#795A09", ["WarningSurfaceBrush"] = "#FAEDC8"
    };

    public static IReadOnlyDictionary<string, string> Dark { get; } = new Dictionary<string, string>
    {
        ["CanvasBrush"] = "#0D1118", ["SurfaceBrush"] = "#161D27", ["PanelRaisedBrush"] = "#1C2531",
        ["FieldBrush"] = "#101722", ["InkBrush"] = "#F1F6FC", ["MutedBrush"] = "#A8B5C5",
        ["LineBrush"] = "#2D3948", ["ControlBrush"] = "#1C2531", ["ControlHoverBrush"] = "#263342",
        ["ControlBorderBrush"] = "#4B5B6D", ["HeaderBrush"] = "#090D13", ["HeaderInkBrush"] = "#F7FAFF",
        ["HeaderMutedBrush"] = "#AAB8C8", ["PrimaryBrush"] = "#168FE0", ["PrimaryHoverBrush"] = "#31A6EF",
        ["AccentSoftBrush"] = "#15364A", ["FocusBrush"] = "#56D7F4", ["SuccessBrush"] = "#43D5B3",
        ["SuccessSurfaceBrush"] = "#123A34", ["DangerBrush"] = "#FF9AA4", ["DangerSurfaceBrush"] = "#47262C",
        ["WarningBrush"] = "#F1CF72", ["WarningSurfaceBrush"] = "#41371D"
    };
}
