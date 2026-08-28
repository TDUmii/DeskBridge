using System.Text.Json;
using DeskBridge.Core.Actions;
using DeskBridge.Core.Models;
using DeskBridge.Core.Services;
using DeskBridge.Host.NativeMessaging;

var settingsStore = new SettingsStore();
var activityLogger = new ActivityLogger();
var registry = ActionRuntime.CreateRegistry();
var transport = new NativeMessagingTransport(Console.OpenStandardInput(), Console.OpenStandardOutput());

while (true)
{
    string? json;
    try
    {
        json = await transport.ReadAsync(CancellationToken.None);
    }
    catch (NativeMessageException exception)
    {
        await transport.WriteAsync(new ActionResponse(1, string.Empty, false, null,
            new DeskBridgeError(ErrorCodes.InvalidRequest, exception.Message)), CancellationToken.None);
        continue;
    }

    if (json is null) break;
    ActionResponse response;
    try
    {
        var request = JsonSerializer.Deserialize<ActionRequest>(json, DeskBridgeJson.Options);
        if (request is null)
        {
            response = new ActionResponse(1, string.Empty, false, null,
                new DeskBridgeError(ErrorCodes.InvalidRequest, "Request JSON is empty."));
        }
        else
        {
            var settings = await settingsStore.LoadAsync();
            if (string.Equals(request.Action, "get_status", StringComparison.OrdinalIgnoreCase))
            {
                response = new ActionResponse(1, request.Id, true, new
                {
                    connected = true,
                    workspace = settings.WorkspacePath,
                    workspaceMode = settings.WorkspaceMode,
                    host = "com.deskbridge.host"
                }, null);
            }
            else if (string.Equals(request.Action, "open_deskbridge", StringComparison.OrdinalIgnoreCase) &&
                     string.IsNullOrWhiteSpace(settings.WorkspacePath))
            {
                var bootstrapContext = ActionRuntime.CreateContext(Path.GetTempPath(), settingsStore, activityLogger);
                response = ActionResponse.FromResult(request.Id,
                    await registry.ExecuteAsync(request, bootstrapContext, CancellationToken.None));
            }
            else if (!settings.WorkspaceMode || string.IsNullOrWhiteSpace(settings.WorkspacePath))
            {
                response = new ActionResponse(1, request.Id, false, null,
                    new DeskBridgeError(ErrorCodes.WorkspaceViolation,
                        "Choose an allowed workspace in DeskBridge before running actions."));
            }
            else
            {
                var context = ActionRuntime.CreateContext(settings.WorkspacePath, settingsStore, activityLogger);
                response = ActionResponse.FromResult(request.Id,
                    await registry.ExecuteAsync(request, context, CancellationToken.None));
            }
        }
    }
    catch (JsonException exception)
    {
        response = new ActionResponse(1, string.Empty, false, null,
            new DeskBridgeError(ErrorCodes.InvalidRequest, $"Malformed JSON: {exception.Message}"));
    }
    catch (Exception exception)
    {
        response = new ActionResponse(1, string.Empty, false, null,
            new DeskBridgeError(ErrorCodes.InternalError, exception.Message));
    }

    await transport.WriteAsync(response, CancellationToken.None);
}
