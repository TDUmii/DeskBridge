using System.Text.Json;
using DeskBridge.Core.Actions;
using DeskBridge.Core.Agent;
using DeskBridge.Core.Models;

namespace DeskBridge.Tests;

public sealed class WorkspaceContextTests
{
    [Fact]
    public async Task WorkspaceModeProvidesBoundedReadOnlyContextWithoutAbsolutePaths()
    {
        using var workspace = new TestWorkspace();
        Directory.CreateDirectory(workspace.PathOf("src"));
        await File.WriteAllTextAsync(workspace.PathOf("src", "app.ts"), "line one\nneedle here\nline three");
        var service = new WorkspaceContextService(workspace.Root);

        var result = await service.ExecuteAsync([
            new BrowserContextRequest("list_directory", ".", Depth: 2),
            new BrowserContextRequest("read_file", "src/app.ts", StartLine: 2, EndLine: 2),
            new BrowserContextRequest("search_workspace", "src", Query: "needle")
        ], CancellationToken.None);
        var json = JsonSerializer.Serialize(result, DeskBridgeJson.Options);

        Assert.Contains("workspace:/src/app.ts", json, StringComparison.Ordinal);
        Assert.Contains("needle here", json, StringComparison.Ordinal);
        Assert.DoesNotContain(workspace.Root, json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("line one", json, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(".env")]
    [InlineData("private.key")]
    [InlineData("credentials.json")]
    public async Task SensitiveFilesAreAlwaysBlocked(string filename)
    {
        using var workspace = new TestWorkspace();
        await File.WriteAllTextAsync(workspace.PathOf(filename), "secret-value");
        var service = new WorkspaceContextService(workspace.Root);

        var error = await Assert.ThrowsAsync<DeskBridgeActionException>(() => service.ExecuteAsync([
            new BrowserContextRequest("read_file", filename)
        ], CancellationToken.None));

        Assert.Equal(ErrorCodes.ActionNotAllowed, error.Code);
        Assert.DoesNotContain("secret-value", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CustomIgnoreAndNoiseFoldersDoNotAppear()
    {
        using var workspace = new TestWorkspace();
        await File.WriteAllTextAsync(workspace.PathOf(".deskbridgeignore"), "private-*\n");
        await File.WriteAllTextAsync(workspace.PathOf("private-notes.txt"), "hidden");
        Directory.CreateDirectory(workspace.PathOf("node_modules"));
        await File.WriteAllTextAsync(workspace.PathOf("node_modules", "package.js"), "hidden");
        await File.WriteAllTextAsync(workspace.PathOf("visible.txt"), "shown");
        var service = new WorkspaceContextService(workspace.Root);

        var json = JsonSerializer.Serialize(await service.ExecuteAsync([
            new BrowserContextRequest("list_directory", ".", Depth: 3)
        ], CancellationToken.None), DeskBridgeJson.Options);

        Assert.Contains("visible.txt", json, StringComparison.Ordinal);
        Assert.DoesNotContain("private-notes", json, StringComparison.Ordinal);
        Assert.DoesNotContain("node_modules", json, StringComparison.Ordinal);

        var ignored = await Assert.ThrowsAsync<DeskBridgeActionException>(() => service.ExecuteAsync([
            new BrowserContextRequest("read_file", "node_modules/package.js")
        ], CancellationToken.None));
        Assert.Equal(ErrorCodes.ActionNotAllowed, ignored.Code);
    }

    [Fact]
    public async Task TraversalAndWriteActionsAreRejected()
    {
        using var workspace = new TestWorkspace();
        var service = new WorkspaceContextService(workspace.Root);

        var traversal = await Assert.ThrowsAsync<DeskBridgeActionException>(() => service.ExecuteAsync([
            new BrowserContextRequest("read_file", "../outside.txt")
        ], CancellationToken.None));
        var write = await Assert.ThrowsAsync<DeskBridgeActionException>(() => service.ExecuteAsync([
            new BrowserContextRequest("write_file", "new.txt")
        ], CancellationToken.None));

        Assert.Equal(ErrorCodes.WorkspaceViolation, traversal.Code);
        Assert.Equal(ErrorCodes.InvalidRequest, write.Code);
    }

    [Fact]
    public async Task WorkspaceAgentPromptAndContextRoundStayWebOnly()
    {
        using var workspace = new TestWorkspace();
        var store = new BrowserAgentStore(workspace.PathOf("store"), new ArtifactInspector());
        var job = store.Create(new AgentRunRequest(workspace.Root, null, "Improve this project.", new AgentRunOptions(), AgentRunMode.WorkspaceContext));
        var claim = store.ClaimNext()!;

        Assert.Equal(AgentRunMode.WorkspaceContext, claim.Mode);
        Assert.False(claim.HasSource);
        Assert.Contains("bounded read-only context channel", claim.Prompt, StringComparison.Ordinal);
        Assert.Contains("Do not suggest Codex", claim.Prompt, StringComparison.Ordinal);
        Assert.DoesNotContain(workspace.Root, claim.Prompt, StringComparison.OrdinalIgnoreCase);

        await store.ReadWorkspaceContextAsync(job.Id, new BrowserContextEnvelope([
            new BrowserContextRequest("workspace_info")
        ]), CancellationToken.None);

        Assert.Equal(1, store.Read(job.Id)!.ContextRound);
        Assert.Equal("Context ready", store.Read(job.Id)!.Stage);
    }

    [Fact]
    public async Task ContextIsModeLockedAndStopsAfterSixRounds()
    {
        using var workspace = new TestWorkspace();
        var store = new BrowserAgentStore(workspace.PathOf("store"), new ArtifactInspector());
        var createJob = store.Create(new AgentRunRequest(workspace.Root, null, "Create a file.", new AgentRunOptions(), AgentRunMode.CreateNew));
        store.ClaimNext();
        await Assert.ThrowsAsync<InvalidOperationException>(() => store.ReadWorkspaceContextAsync(createJob.Id,
            new BrowserContextEnvelope([new BrowserContextRequest("workspace_info")]), CancellationToken.None));

        store.Cancel(createJob.Id);
        var contextJob = store.Create(new AgentRunRequest(workspace.Root, null, "Review the project.", new AgentRunOptions(), AgentRunMode.WorkspaceContext));
        store.ClaimNext();
        for (var round = 0; round < 6; round++)
        {
            await store.ReadWorkspaceContextAsync(contextJob.Id,
                new BrowserContextEnvelope([new BrowserContextRequest("workspace_info")]), CancellationToken.None);
        }

        var limit = await Assert.ThrowsAsync<InvalidOperationException>(() => store.ReadWorkspaceContextAsync(contextJob.Id,
            new BrowserContextEnvelope([new BrowserContextRequest("workspace_info")]), CancellationToken.None));
        Assert.Contains("maximum", limit.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(6, store.Read(contextJob.Id)!.ContextRound);
    }
}
