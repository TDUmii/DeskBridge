using DeskBridge.Core.Actions;
using DeskBridge.Core.Security;
using DeskBridge.Core.Services;

namespace DeskBridge.Tests;

internal sealed class TestWorkspace : IDisposable
{
    public TestWorkspace()
    {
        Root = Path.Combine(Path.GetTempPath(), "DeskBridge.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Root);
        Context = new ActionContext(new WorkspaceGuard(Root),
            new SettingsStore(Path.Combine(Root, "settings.json")),
            new ActivityLogger(Path.Combine(Root, "activity.jsonl")), new HttpClient());
    }

    public string Root { get; }
    public ActionContext Context { get; }
    public string PathOf(params string[] parts) => Path.Combine([Root, .. parts]);

    public void Dispose()
    {
        if (Directory.Exists(Root) && Path.GetFullPath(Root).StartsWith(Path.Combine(Path.GetTempPath(), "DeskBridge.Tests"), StringComparison.OrdinalIgnoreCase))
            Directory.Delete(Root, true);
    }
}
