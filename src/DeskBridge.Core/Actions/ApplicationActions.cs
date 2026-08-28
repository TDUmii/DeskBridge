using System.Diagnostics;
using System.Text.Json;
using DeskBridge.Core.Models;

namespace DeskBridge.Core.Actions;

public sealed class ApplicationRegistry
{
    private readonly IReadOnlyDictionary<string, string> _applications = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["vscode"] = "code",
        ["notepad"] = "notepad.exe",
        ["calculator"] = "calc.exe",
        ["terminal"] = "wt.exe",
        ["explorer"] = "explorer.exe"
    };

    public bool TryResolve(string alias, out string program) => _applications.TryGetValue(alias, out program!);
}

public sealed class OpenFolderAction : IDeskBridgeAction
{
    public string Name => "open_folder";

    public Task<ActionResult> ExecuteAsync(JsonElement arguments, ActionContext context, CancellationToken cancellationToken)
    {
        var path = context.WorkspaceGuard.EnsureInside(arguments.RequiredString("path"));
        if (!Directory.Exists(path))
        {
            return Task.FromResult(ActionResult.Fail(ErrorCodes.FileNotFound, "Folder does not exist."));
        }

        Process.Start(new ProcessStartInfo("explorer.exe") { UseShellExecute = true, ArgumentList = { path } });
        return Task.FromResult(ActionResult.Ok(new { path }));
    }
}

public sealed class OpenAppAction(ApplicationRegistry registry) : IDeskBridgeAction
{
    public string Name => "open_app";

    public Task<ActionResult> ExecuteAsync(JsonElement arguments, ActionContext context, CancellationToken cancellationToken)
    {
        var alias = arguments.RequiredString("app");
        if (!registry.TryResolve(alias, out var program))
        {
            return Task.FromResult(ActionResult.Fail(ErrorCodes.AppNotFound, "Application alias is not allowed or installed."));
        }

        try
        {
            Process.Start(new ProcessStartInfo(program) { UseShellExecute = true });
            return Task.FromResult(ActionResult.Ok(new { app = alias }));
        }
        catch (Exception exception) when (exception is System.ComponentModel.Win32Exception or FileNotFoundException)
        {
            return Task.FromResult(ActionResult.Fail(ErrorCodes.AppNotFound, $"Could not open {alias}: {exception.Message}"));
        }
    }
}

public sealed class OpenProjectAction(ApplicationRegistry registry) : IDeskBridgeAction
{
    public string Name => "open_project";

    public Task<ActionResult> ExecuteAsync(JsonElement arguments, ActionContext context, CancellationToken cancellationToken)
    {
        var path = context.WorkspaceGuard.EnsureInside(arguments.RequiredString("path"));
        var editor = arguments.RequiredString("editor");
        if (!Directory.Exists(path))
        {
            return Task.FromResult(ActionResult.Fail(ErrorCodes.FileNotFound, "Project folder does not exist."));
        }

        if (!string.Equals(editor, "vscode", StringComparison.OrdinalIgnoreCase) || !registry.TryResolve(editor, out var program))
        {
            return Task.FromResult(ActionResult.Fail(ErrorCodes.AppNotFound, "Only the vscode editor alias is supported in V1."));
        }

        var startInfo = new ProcessStartInfo(program) { UseShellExecute = true };
        startInfo.ArgumentList.Add(path);
        Process.Start(startInfo);
        return Task.FromResult(ActionResult.Ok(new { path, editor }));
    }
}

public sealed class OpenInBrowserAction : IDeskBridgeAction
{
    public string Name => "open_in_browser";

    public Task<ActionResult> ExecuteAsync(JsonElement arguments, ActionContext context, CancellationToken cancellationToken)
    {
        var path = context.WorkspaceGuard.EnsureInside(arguments.RequiredString("path"), false);
        if (!File.Exists(path))
        {
            return Task.FromResult(ActionResult.Fail(ErrorCodes.FileNotFound, "File does not exist."));
        }

        Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        return Task.FromResult(ActionResult.Ok(new { path }));
    }
}

public sealed class PreviewWebAction : IDeskBridgeAction
{
    public string Name => "preview_web";

    public Task<ActionResult> ExecuteAsync(JsonElement arguments, ActionContext context, CancellationToken cancellationToken)
    {
        var root = context.WorkspaceGuard.EnsureInside(arguments.RequiredString("rootPath"));
        var entry = arguments.TryGetProperty("entryFile", out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? "index.html" : "index.html";
        var path = context.WorkspaceGuard.ResolveRelative(root, entry);
        if (!File.Exists(path))
        {
            return Task.FromResult(ActionResult.Fail(ErrorCodes.FileNotFound, "Website entry file does not exist."));
        }

        Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        return Task.FromResult(ActionResult.Ok(new { rootPath = root, entryFile = entry, path }));
    }
}
