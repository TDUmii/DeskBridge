using System.Text.Json;
using DeskBridge.Core.Actions;
using DeskBridge.Core.Models;
using DeskBridge.Core.Services;
using DeskBridge.Host.NativeMessaging;
using DeskBridge.Core.Skills;
using DeskBridge.Core.Agent;

var settingsStore = new SettingsStore();
var activityLogger = new ActivityLogger();
var registry = ActionRuntime.CreateRegistry();
var transport = new NativeMessagingTransport(Console.OpenStandardInput(), Console.OpenStandardOutput());
var webAgent = new WebAgentNativeController(new BrowserAgentStore());

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
            if (WebAgentNativeController.Actions.Contains(request.Action))
            {
                response = ActionResponse.FromResult(request.Id, await webAgent.ExecuteAsync(request, CancellationToken.None));
            }
            else if (string.Equals(request.Action, "get_status", StringComparison.OrdinalIgnoreCase))
            {
                response = new ActionResponse(1, request.Id, true, new
                {
                    connected = true,
                    workspace = settings.WorkspacePath,
                    workspaceMode = settings.WorkspaceMode,
                    themeMode = settings.ThemeMode,
                    skills = SkillCatalog.All.Select(skill => new { skill.Id, skill.Name, enabled = SkillCatalog.IsEnabled(settings, skill.Id) }),
                    host = "com.deskbridge.host"
                }, null);
            }
            else if (string.Equals(request.Action, "get_skill_profile", StringComparison.OrdinalIgnoreCase))
            {
                var profileContext = ActionRuntime.CreateContext(settings.WorkspacePath ?? Path.GetTempPath(), settingsStore, activityLogger);
                response = ActionResponse.FromResult(request.Id,
                    await registry.ExecuteAsync(request, profileContext, CancellationToken.None));
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
