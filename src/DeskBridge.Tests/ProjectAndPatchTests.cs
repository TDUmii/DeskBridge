using System.Text.Json;
using DeskBridge.Core.Actions;
using DeskBridge.Core.Models;
using DeskBridge.Core.Projects;

namespace DeskBridge.Tests;

public sealed class ProjectAndPatchTests
{
    [Fact]
    public async Task CreateProjectWritesNestedFiles()
    {
        using var workspace = new TestWorkspace();
        var root = workspace.PathOf("DemoWebsite");
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(new
        {
            rootPath = root, projectType = "static-web",
            files = new[] { new { path = "index.html", content = "<h1>Hi</h1>" }, new { path = "css/style.css", content = "body{}" }, new { path = "js/script.js", content = "" } }
        }));
        var result = await new CreateProjectAction().ExecuteAsync(json.RootElement, workspace.Context, CancellationToken.None);
        Assert.True(result.Success);
        Assert.True(File.Exists(Path.Combine(root, "css", "style.css")));
        Assert.Contains(".deskbridge/", await File.ReadAllTextAsync(Path.Combine(root, ".gitignore")));
    }

    [Theory]
    [InlineData("../evil.txt")]
    [InlineData("C:\\evil.txt")]
    public async Task CreateProjectBlocksUnsafeChildPath(string path)
    {
        using var workspace = new TestWorkspace();
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(new { rootPath = workspace.PathOf("Project"), projectType = "generic", files = new[] { new { path, content = "x" } } }));
        var error = await Assert.ThrowsAsync<DeskBridgeActionException>(() => new CreateProjectAction().ExecuteAsync(json.RootElement, workspace.Context, CancellationToken.None));
        Assert.Equal(ErrorCodes.ProjectPathInvalid, error.Code);
    }

    [Fact]
    public async Task CreateProjectRejectsDuplicatePath()
    {
        using var workspace = new TestWorkspace();
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(new { rootPath = workspace.PathOf("Project"), projectType = "generic", files = new[] { new { path = "a.txt", content = "1" }, new { path = "A.txt", content = "2" } } }));
        var error = await Assert.ThrowsAsync<DeskBridgeActionException>(() => new CreateProjectAction().ExecuteAsync(json.RootElement, workspace.Context, CancellationToken.None));
        Assert.Equal(ErrorCodes.ProjectPathInvalid, error.Code);
    }

    [Fact]
    public async Task PatchRequiresExactlyOneMatch()
    {
        using var workspace = new TestWorkspace();
        var path = workspace.PathOf("style.css");
        await File.WriteAllTextAsync(path, "one target three");
        var success = await Patch(path, "target", "two", workspace.Context);
        Assert.True(success.Success);
        Assert.Equal("one two three", await File.ReadAllTextAsync(path));
        Assert.Equal(ErrorCodes.PatchTargetNotFound, (await Patch(path, "missing", "x", workspace.Context)).Error?.Code);
        await File.WriteAllTextAsync(path, "same same");
        Assert.Equal(ErrorCodes.PatchTargetNotUnique, (await Patch(path, "same", "x", workspace.Context)).Error?.Code);
    }

    private static async Task<ActionResult> Patch(string path, string oldText, string newText, ActionContext context)
    {
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(new { path, replacements = new[] { new { oldText, newText } } }));
        return await new PatchFileAction().ExecuteAsync(json.RootElement, context, CancellationToken.None);
    }
}
