using DeskBridge.Core.Actions;
using DeskBridge.Core.Models;
using DeskBridge.Core.Services;

namespace DeskBridge.Core.Security;

public static class PermissionCatalog
{
    private static readonly HashSet<string> SafeActions = new(StringComparer.OrdinalIgnoreCase)
    {
        "get_status", "get_skill_profile", "open_deskbridge", "read_file", "list_folder", "inspect_image", "get_active_window"
    };

    public static async Task<bool> IsAllowedAsync(
        ActionRequest request,
        SettingsStore settingsStore,
        IPermissionService permissionService,
        CancellationToken cancellationToken)
    {
        var settings = await settingsStore.LoadAsync(cancellationToken).ConfigureAwait(false);
        var defaultPolicy = SafeActions.Contains(request.Action) ? "allowed" : "ask";
        var policy = settings.Permissions.GetValueOrDefault(request.Action, defaultPolicy);
        if (string.Equals(policy, "allowed", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (string.Equals(policy, "denied", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return await permissionService.RequestAsync(ActionDescription.CreatePermissionRequest(request), cancellationToken)
            .ConfigureAwait(false);
    }
}
