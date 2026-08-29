using System.Net;
using System.Text;
using System.Text.Json;
using DeskBridge.Core.Agent;

namespace DeskBridge.Tests;

public sealed class AgentTests
{
    [Fact]
    public async Task AgentRunDownloadsInspectsAndPublishesAcceptedCandidate()
    {
        using var workspace = new TestWorkspace();
        var source = workspace.PathOf("brief.txt");
        await File.WriteAllTextAsync(source, "Keep this original.");
        var fake = new CompletingAgentClient();
        var service = new AgentRunService(fake);

        var result = await service.RunAsync(new AgentRunRequest(workspace.Root, source, "Create a polished Markdown result.",
            new AgentRunOptions("gpt-5.6-luna", "low", 4, 12, 4_000)), null, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal("completed", result.Status);
        Assert.Equal(95, result.Score);
        Assert.NotNull(result.BestArtifactPath);
        Assert.True(File.Exists(result.BestArtifactPath));
        Assert.Contains("DeskBridge Results", result.BestArtifactPath, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("# Improved result\n\nVerified locally.", await File.ReadAllTextAsync(result.BestArtifactPath!));
        Assert.Equal("Keep this original.", await File.ReadAllTextAsync(source));
        Assert.True(File.Exists(Path.Combine(result.RunDirectory, "task.json")));
        Assert.True(File.Exists(Path.Combine(result.RunDirectory, "result.json")));
        Assert.Equal(66, result.Usage.TotalTokens);
    }

    [Fact]
    public async Task AgentCannotWriteOverOriginalWorkspaceFile()
    {
        using var workspace = new TestWorkspace();
        var source = workspace.PathOf("protected.txt");
        await File.WriteAllTextAsync(source, "original");
        var fake = new OverwriteAttemptClient(source);
        var service = new AgentRunService(fake);

        var result = await service.RunAsync(new AgentRunRequest(workspace.Root, source, "Improve it.",
            new AgentRunOptions("gpt-5.6-luna", "low", 2, 4, 2_000)), null, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("failed", result.Status);
        Assert.Equal("original", await File.ReadAllTextAsync(source));
        Assert.Contains("Agent tools may access only", fake.LastToolOutput, StringComparison.Ordinal);
    }

    [Fact]
    public async Task WindowsApiKeyStoreEncryptsAndRoundTripsForCurrentUser()
    {
        using var workspace = new TestWorkspace();
        var path = workspace.PathOf("secret.bin");
        var store = new WindowsApiKeyStore(path);

        await store.SaveAsync("sk-test-deskbridge-roundtrip");

        Assert.True(store.HasKey);
        Assert.Equal("sk-test-deskbridge-roundtrip", await store.LoadAsync());
        Assert.DoesNotContain("sk-test", Encoding.UTF8.GetString(await File.ReadAllBytesAsync(path)), StringComparison.Ordinal);
        store.Delete();
        Assert.False(File.Exists(path));
    }

    [Fact]
    public async Task ResponsesClientSendsOfficialToolLoopShape()
    {
        var handler = new RecordingHandler();
        var client = new OpenAIResponsesClient(new HttpClient(handler), new StaticKeyStore());
        var options = new AgentRunOptions("gpt-5.6-luna", "low", 4, 24, 6_000);

        var initial = await client.CreateInitialAsync("instructions", "prompt", "file-input", options, CancellationToken.None);
        await client.ContinueAsync("instructions", initial.Id, [new ToolOutput("call-1", "{\"success\":true}")], options, CancellationToken.None);

        Assert.Equal(2, handler.Payloads.Count);
        using var first = JsonDocument.Parse(handler.Payloads[0]);
        Assert.Equal("gpt-5.6-luna", first.RootElement.GetProperty("model").GetString());
        Assert.Equal("file-input", first.RootElement.GetProperty("input")[0].GetProperty("content")[1].GetProperty("file_id").GetString());
        Assert.Contains(first.RootElement.GetProperty("tools").EnumerateArray(), tool => tool.GetProperty("type").GetString() == "code_interpreter");
        using var second = JsonDocument.Parse(handler.Payloads[1]);
        Assert.Equal("resp-1", second.RootElement.GetProperty("previous_response_id").GetString());
        Assert.Equal("function_call_output", second.RootElement.GetProperty("input")[0].GetProperty("type").GetString());
    }

    private static ResponseToolCall Call(string id, string name, object arguments)
    {
        using var document = JsonDocument.Parse(JsonSerializer.Serialize(arguments));
        return new ResponseToolCall(id, name, document.RootElement.Clone());
    }

    private sealed class CompletingAgentClient : IOpenAIResponsesClient
    {
        public Task TestConnectionAsync(string model, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<string> UploadFileAsync(string path, CancellationToken cancellationToken) => Task.FromResult("file-input");
        public Task<AgentResponse> CreateInitialAsync(string instructions, string prompt, string inputFileId, AgentRunOptions options, CancellationToken cancellationToken) =>
            Task.FromResult(new AgentResponse("resp-1", "completed", string.Empty,
                [Call("call-save", "save_generated_file", new { file_id = "file-output", filename = "result.md", claimed_score = 95, summary = "All requested content is present." })],
                [], new AgentUsage(20, 10, 30), null));
        public Task<AgentResponse> ContinueAsync(string instructions, string previousResponseId, IReadOnlyList<ToolOutput> outputs, AgentRunOptions options, CancellationToken cancellationToken)
        {
            using var output = JsonDocument.Parse(outputs.Single().Output);
            var path = output.RootElement.GetProperty("path").GetString();
            return Task.FromResult(new AgentResponse("resp-2", "completed", string.Empty,
                [Call("call-complete", "complete_task", new { best_path = path, score = 95, summary = "Verified result.", requirements_met = new[] { "Polished Markdown" }, remaining_issues = Array.Empty<string>() })],
                [], new AgentUsage(24, 12, 36), null));
        }
        public async Task DownloadFileAsync(string fileId, string destination, CancellationToken cancellationToken)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            await File.WriteAllTextAsync(destination, "# Improved result\n\nVerified locally.", cancellationToken);
        }
    }

    private sealed class OverwriteAttemptClient(string path) : IOpenAIResponsesClient
    {
        public string LastToolOutput { get; private set; } = string.Empty;
        public Task TestConnectionAsync(string model, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<string> UploadFileAsync(string source, CancellationToken cancellationToken) => Task.FromResult("file-input");
        public Task<AgentResponse> CreateInitialAsync(string instructions, string prompt, string inputFileId, AgentRunOptions options, CancellationToken cancellationToken) =>
            Task.FromResult(new AgentResponse("resp-1", "completed", string.Empty,
                [Call("call-write", "write_text_file", new { path, content = "overwritten", overwrite = true })], [], new AgentUsage(1, 1, 2), null));
        public Task<AgentResponse> ContinueAsync(string instructions, string previousResponseId, IReadOnlyList<ToolOutput> outputs, AgentRunOptions options, CancellationToken cancellationToken)
        {
            LastToolOutput = outputs.Single().Output;
            return Task.FromResult(new AgentResponse("resp-2", "completed", string.Empty,
                [Call("call-write-2", "write_text_file", new { path, content = "still overwritten", overwrite = true })], [], new AgentUsage(1, 1, 2), null));
        }
        public Task DownloadFileAsync(string fileId, string destination, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class StaticKeyStore : IApiKeyStore
    {
        public bool HasKey => true;
        public Task SaveAsync(string apiKey, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<string?> LoadAsync(CancellationToken cancellationToken = default) => Task.FromResult<string?>("sk-test");
        public void Delete() { }
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        public List<string> Payloads { get; } = [];
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Payloads.Add(await request.Content!.ReadAsStringAsync(cancellationToken));
            var responseNumber = Payloads.Count;
            var json = JsonSerializer.Serialize(new
            {
                id = $"resp-{responseNumber}", status = "completed", output = Array.Empty<object>(),
                usage = new { input_tokens = 1, output_tokens = 1, total_tokens = 2 }
            });
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
        }
    }
}
