using System.Text.Json;
using DeskBridge.Core.Models;
using DeskBridge.Core.Security;
using DeskBridge.Core.Services;

namespace DeskBridge.Core.Actions;

public interface IDeskBridgeAction
{
    string Name { get; }

    Task<ActionResult> ExecuteAsync(
        JsonElement arguments,
        ActionContext context,
        CancellationToken cancellationToken);
}

public sealed record ActionContext(
    WorkspaceGuard WorkspaceGuard,
    SettingsStore Settings,
    ActivityLogger ActivityLogger,
    HttpClient HttpClient);

public sealed class DeskBridgeActionException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}
