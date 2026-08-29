using System.Text;
using System.Text.Json;
using DeskBridge.Core.Agent;
using DeskBridge.Core.Models;

namespace DeskBridge.Tests;

public sealed class AgentTests
{
    [Fact]
    public async Task WebStorePreservesSourceAndPublishesAcceptedCandidate()
    {
        using var workspace = new TestWorkspace();
        var source = workspace.PathOf("brief.txt");
        await File.WriteAllTextAsync(source, "Keep this original.");
        var store = Store(workspace);
        var job = store.Create(new AgentRunRequest(workspace.Root, source, "Create a polished Markdown result.", new AgentRunOptions()));
        var claim = store.ClaimNext();

        Assert.NotNull(claim);
        Assert.Equal("GPT-5.6 Sol", claim.RequiredModel);
        Assert.Equal("High", claim.RequiredReasoning);
        Assert.Contains("Do not suggest Codex", claim.Prompt, StringComparison.Ordinal);
        Assert.Contains("OpenAI API", claim.Prompt, StringComparison.Ordinal);
        Assert.Contains(claim.CandidateToken, claim.Prompt, StringComparison.Ordinal);

        var candidateName = $"finished-{claim.CandidateToken}.md";
        var downloaded = workspace.PathOf(candidateName);
        await File.WriteAllTextAsync(downloaded, "# Finished result\n\nVerified locally.");
        var result = await store.InspectCandidateAsync(job.Id, downloaded,
            new BrowserCandidateAssessment(candidateName, 95, "All requested content is present.", ["Polished Markdown"], []), CancellationToken.None);

        Assert.True(result.Accepted);
        Assert.True(result.Terminal);
        Assert.Equal("completed", result.Status);
        Assert.True(File.Exists(result.LocalPath));
        Assert.Contains("DeskBridge Results", result.LocalPath, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("Keep this original.", await File.ReadAllTextAsync(source));
        Assert.Equal("Keep this original.", await File.ReadAllTextAsync(job.PreservedSourcePath));
    }

    [Fact]
    public async Task LocalInspectionRequestsAnotherWebPassBelowCompletionGate()
    {
        using var workspace = new TestWorkspace();
        var source = workspace.PathOf("source.txt");
        await File.WriteAllTextAsync(source, "source");
        var store = Store(workspace);
        var job = store.Create(new AgentRunRequest(workspace.Root, source, "Improve it.", new AgentRunOptions(3)));
        var claim = store.ClaimNext()!;
        var candidateName = $"draft-{claim.CandidateToken}.md";
        var downloaded = workspace.PathOf(candidateName);
        await File.WriteAllTextAsync(downloaded, "draft");

        var result = await store.InspectCandidateAsync(job.Id, downloaded,
            new BrowserCandidateAssessment(candidateName, 72, "Incomplete.", ["A draft exists"], ["Missing final detail"]), CancellationToken.None);
        var updated = store.Read(job.Id)!;

        Assert.False(result.Accepted);
        Assert.False(result.Terminal);
        Assert.Equal(2, updated.Iteration);
        Assert.Contains("downloaded and inspected", result.FollowUpPrompt, StringComparison.Ordinal);
        Assert.Contains("Missing final detail", result.FollowUpPrompt, StringComparison.Ordinal);
        Assert.Contains("same safety token", result.FollowUpPrompt, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CandidateWithoutRunTokenIsRejected()
    {
        using var workspace = new TestWorkspace();
        var source = workspace.PathOf("source.txt");
        await File.WriteAllTextAsync(source, "source");
        var store = Store(workspace);
        var job = store.Create(new AgentRunRequest(workspace.Root, source, "Improve it.", new AgentRunOptions()));
        store.ClaimNext();
        var downloaded = workspace.PathOf("untrusted.md");
        await File.WriteAllTextAsync(downloaded, "not this run");

        await Assert.ThrowsAsync<InvalidOperationException>(() => store.InspectCandidateAsync(job.Id, downloaded,
            new BrowserCandidateAssessment("untrusted.md", 100, "Claimed complete.", [], []), CancellationToken.None));
    }

    [Fact]
    public void SourceChunksRoundTripWithoutGivingBrowserAWorkspacePath()
    {
        using var workspace = new TestWorkspace();
        var source = workspace.PathOf("unicode.txt");
        File.WriteAllText(source, "Xin chào DeskBridge", new UTF8Encoding(false));
        var store = Store(workspace);
        var job = store.Create(new AgentRunRequest(workspace.Root, source, "Check it.", new AgentRunOptions()));
        store.ClaimNext();

        using var json = JsonDocument.Parse(JsonSerializer.Serialize(store.ReadSourceChunk(job.Id, 0, 7), DeskBridgeJson.Options));
        var root = json.RootElement;
        var first = Convert.FromBase64String(root.GetProperty("bytes").GetString()!);

        Assert.NotEmpty(first);
        Assert.False(root.TryGetProperty("path", out _));
        Assert.Equal(7, root.GetProperty("nextOffset").GetInt64());
    }

    [Fact]
    public async Task NativeControllerRejectsMalformedAssessment()
    {
        using var workspace = new TestWorkspace();
        var controller = new WebAgentNativeController(Store(workspace));
        using var arguments = JsonDocument.Parse("""{"runId":"missing","downloadedPath":"x","assessment":{"candidateFile":"","score":95,"summary":"x","requirementsMet":[],"remainingIssues":[]}}""");
        var response = await controller.ExecuteAsync(new ActionRequest
        {
            Version = 1, Id = "test", Action = "web_agent_candidate", Arguments = arguments.RootElement.Clone()
        }, CancellationToken.None);

        Assert.False(response.Success);
        Assert.Contains("non-empty", response.Error!.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void CancelledRunIsTerminalAndCannotBeClaimed()
    {
        using var workspace = new TestWorkspace();
        var source = workspace.PathOf("source.txt");
        File.WriteAllText(source, "source");
        var store = Store(workspace);
        var job = store.Create(new AgentRunRequest(workspace.Root, source, "Improve it.", new AgentRunOptions()));

        var cancelled = store.Cancel(job.Id);

        Assert.Equal("cancelled", cancelled.Status);
        Assert.Null(store.ClaimNext());
    }

    private static BrowserAgentStore Store(TestWorkspace workspace) => new(workspace.PathOf("store"), new ArtifactInspector());
}
