using System.Diagnostics;
using System.Text.Json;
using DeskBridge.Core.Models;
using DeskBridge.Core.Services;

namespace DeskBridge.Core.Actions;

public sealed class ActionRegistry(
    IEnumerable<IDeskBridgeAction> actions,
    IPermissionService permissionService)
{
    private readonly IReadOnlyDictionary<string, IDeskBridgeAction> _actions = actions
        .ToDictionary(action => action.Name, StringComparer.OrdinalIgnoreCase);

    public IReadOnlyCollection<string> Names => _actions.Keys;

    public async Task<ActionResult> ExecuteAsync(
        ActionRequest request,
        ActionContext context,
        CancellationToken cancellationToken)
    {
        if (request.Version != 1 || string.IsNullOrWhiteSpace(request.Id) ||
            string.IsNullOrWhiteSpace(request.Action) || request.Arguments.ValueKind != JsonValueKind.Object)
        {
            return ActionResult.Fail(ErrorCodes.InvalidRequest, "Request must use protocol version 1 and include id, action, and object arguments.");
        }

        if (!_actions.TryGetValue(request.Action, out var action))
        {
            return ActionResult.Fail(ErrorCodes.UnknownAction, $"Action '{request.Action}' is not supported.");
        }

        var stopwatch = Stopwatch.StartNew();
        var target = ActionDescription.GetTarget(request.Action, request.Arguments);
        ActionResult result;

        try
        {
            if (!await PermissionCatalog.IsAllowedAsync(
                    request,
                    context.Settings,
                    permissionService,
                    cancellationToken).ConfigureAwait(false))
            {
                result = ActionResult.Fail(ErrorCodes.PermissionDenied, "The user did not allow this action.");
            }
            else
            {
                result = await action.ExecuteAsync(request.Arguments, context, cancellationToken)
                    .ConfigureAwait(false);
            }
        }
        catch (DeskBridgeActionException exception)
        {
            result = ActionResult.Fail(exception.Code, exception.Message);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            result = ActionResult.Fail(ErrorCodes.ExecutionFailed, "The action was cancelled.");
        }
        catch (Exception exception)
        {
            result = ActionResult.Fail(ErrorCodes.InternalError, $"Action failed: {exception.Message}");
        }

        stopwatch.Stop();
        await context.ActivityLogger.WriteAsync(
            new ActivityEntry(DateTimeOffset.UtcNow, request.Action, target,
                result.Success ? "success" : result.Error?.Code ?? "error", stopwatch.ElapsedMilliseconds),
            cancellationToken).ConfigureAwait(false);
        return result;
    }
}

internal static class ActionDescription
{
    private static readonly string[] PathKeys = ["path", "rootPath", "destination", "workingDirectory", "source"];

    public static string GetTarget(string action, JsonElement arguments)
    {
        foreach (var key in PathKeys)
        {
            if (arguments.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.String)
            {
                return value.GetString() ?? action;
            }
        }

        if (arguments.TryGetProperty("program", out var program) && program.ValueKind == JsonValueKind.String)
        {
            return program.GetString() ?? action;
        }

        return action;
    }

    public static PermissionRequest CreatePermissionRequest(ActionRequest request)
    {
        var target = GetTarget(request.Action, request.Arguments);
        var items = new List<string>();
        if (request.Arguments.TryGetProperty("files", out var files) && files.ValueKind == JsonValueKind.Array)
        {
            foreach (var file in files.EnumerateArray().Take(50))
            {
                if (file.TryGetProperty("path", out var path) && path.ValueKind == JsonValueKind.String)
                {
                    items.Add(path.GetString() ?? string.Empty);
                }
            }
        }

        if (request.Arguments.TryGetProperty("args", out var args) && args.ValueKind == JsonValueKind.Array)
        {
            items.AddRange(args.EnumerateArray().Where(value => value.ValueKind == JsonValueKind.String)
                .Take(50).Select(value => value.GetString() ?? string.Empty));
        }

        return new PermissionRequest(request.Id, request.Action,
            $"ChatGPT wants to run {request.Action}.", target, items);
    }
}
