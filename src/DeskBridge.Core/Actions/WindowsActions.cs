using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Windows.Forms;
using DeskBridge.Core.Models;
using DeskBridge.Core.Services;

namespace DeskBridge.Core.Actions;

public sealed class CaptureScreenAction : IDeskBridgeAction
{
    public string Name => "capture_screen";

    public Task<ActionResult> ExecuteAsync(JsonElement arguments, ActionContext context, CancellationToken cancellationToken)
    {
        var bounds = Screen.PrimaryScreen?.Bounds ?? throw new DeskBridgeActionException(ErrorCodes.ExecutionFailed,
            "Primary monitor could not be determined.");
        var folder = Path.Combine(Path.GetTempPath(), "DeskBridge");
        Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, $"screen-{DateTimeOffset.UtcNow:yyyyMMdd-HHmmss-fff}.png");
        using var bitmap = new Bitmap(bounds.Width, bounds.Height);
        using (var graphics = Graphics.FromImage(bitmap))
        {
            graphics.CopyFromScreen(bounds.Location, Point.Empty, bounds.Size);
        }

        bitmap.Save(path, System.Drawing.Imaging.ImageFormat.Png);
        return Task.FromResult(ActionResult.Ok(new
        {
            path,
            width = bounds.Width,
            height = bounds.Height,
            timestamp = DateTimeOffset.UtcNow
        }));
    }
}

public sealed class GetActiveWindowAction : IDeskBridgeAction
{
    public string Name => "get_active_window";

    public Task<ActionResult> ExecuteAsync(JsonElement arguments, ActionContext context, CancellationToken cancellationToken)
    {
        var handle = NativeMethods.GetForegroundWindow();
        if (handle == IntPtr.Zero)
        {
            return Task.FromResult(ActionResult.Fail(ErrorCodes.ExecutionFailed, "No active window was found."));
        }

        _ = NativeMethods.GetWindowThreadProcessId(handle, out var processId);
        var length = NativeMethods.GetWindowTextLength(handle);
        var title = new StringBuilder(length + 1);
        _ = NativeMethods.GetWindowText(handle, title, title.Capacity);
        string processName;
        try
        {
            processName = Process.GetProcessById((int)processId).ProcessName;
        }
        catch (ArgumentException)
        {
            processName = "unknown";
        }

        return Task.FromResult(ActionResult.Ok(new { title = title.ToString(), processName, processId }));
    }

    private static class NativeMethods
    {
        [DllImport("user32.dll")]
        public static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        public static extern int GetWindowTextLength(IntPtr handle);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        public static extern int GetWindowText(IntPtr handle, StringBuilder text, int count);

        [DllImport("user32.dll")]
        public static extern uint GetWindowThreadProcessId(IntPtr handle, out uint processId);
    }
}
