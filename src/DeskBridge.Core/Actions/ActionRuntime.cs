using DeskBridge.Core.Downloads;
using DeskBridge.Core.Images;
using DeskBridge.Core.Projects;
using DeskBridge.Core.Security;
using DeskBridge.Core.Services;

namespace DeskBridge.Core.Actions;

public static class ActionRuntime
{
    public static ActionRegistry CreateRegistry(IPermissionService? permissionService = null)
    {
        var applicationRegistry = new ApplicationRegistry();
        IDeskBridgeAction[] actions =
        [
            new GetStatusAction(), new OpenDeskBridgeAction(), new ReadFileAction(), new WriteFileAction(), new CreateFileAction(), new CreateFolderAction(),
            new ListFolderAction(), new PatchFileAction(), new CreateProjectAction(), new UpdateProjectAction(),
            new GetClipboardAction(), new SetClipboardAction(), new OpenFolderAction(),
            new OpenAppAction(applicationRegistry), new OpenProjectAction(applicationRegistry),
            new OpenInBrowserAction(), new PreviewWebAction(), new RunCommandAction(new CommandRunner()),
            new CaptureScreenAction(), new GetActiveWindowAction(),
            new DownloadAssetAction(new SecureImageDownloader(new NetworkGuard())), new ImportAssetAction(),
            new InspectImageAction(), new ResizeImageAction(), new CompressImageAction(), new ConvertImageAction()
        ];
        return new ActionRegistry(actions, permissionService ?? new NamedPipePermissionService());
    }

    public static ActionContext CreateContext(string workspace, SettingsStore? settings = null, ActivityLogger? logger = null) =>
        new(new WorkspaceGuard(workspace), settings ?? new SettingsStore(), logger ?? new ActivityLogger(), new HttpClient());
}
