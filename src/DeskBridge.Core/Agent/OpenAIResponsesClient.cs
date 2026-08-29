using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace DeskBridge.Core.Agent;

public interface IOpenAIResponsesClient
{
    Task TestConnectionAsync(string model, CancellationToken cancellationToken);
    Task<string> UploadFileAsync(string path, CancellationToken cancellationToken);
    Task<AgentResponse> CreateInitialAsync(string instructions, string prompt, string inputFileId, AgentRunOptions options, CancellationToken cancellationToken);
    Task<AgentResponse> ContinueAsync(string instructions, string previousResponseId, IReadOnlyList<ToolOutput> outputs, AgentRunOptions options, CancellationToken cancellationToken);
    Task DownloadFileAsync(string fileId, string destination, CancellationToken cancellationToken);
}

public sealed class OpenAIResponsesClient(HttpClient httpClient, IApiKeyStore keyStore) : IOpenAIResponsesClient
{
    private static readonly Uri BaseUri = new("https://api.openai.com/v1/");

    public async Task TestConnectionAsync(string model, CancellationToken cancellationToken)
    {
        using var request = await CreateRequestAsync(HttpMethod.Get, $"models/{Uri.EscapeDataString(model)}", cancellationToken).ConfigureAwait(false);
        using var response = await httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        await EnsureSuccessAsync(response, cancellationToken).ConfigureAwait(false);
    }

    public async Task<string> UploadFileAsync(string path, CancellationToken cancellationToken)
    {
        using var request = await CreateRequestAsync(HttpMethod.Post, "files", cancellationToken).ConfigureAwait(false);
        using var content = new MultipartFormDataContent();
        content.Add(new StringContent("user_data"), "purpose");
        var stream = File.OpenRead(path);
        var fileContent = new StreamContent(stream);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        content.Add(fileContent, "file", Path.GetFileName(path));
        request.Content = content;
        using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        await EnsureSuccessAsync(response, cancellationToken).ConfigureAwait(false);
        await using var responseStream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var document = await JsonDocument.ParseAsync(responseStream, cancellationToken: cancellationToken).ConfigureAwait(false);
        return document.RootElement.GetProperty("id").GetString() ?? throw new InvalidOperationException("OpenAI upload response did not include a file ID.");
    }

    public Task<AgentResponse> CreateInitialAsync(string instructions, string prompt, string inputFileId, AgentRunOptions options, CancellationToken cancellationToken) =>
        CreateResponseAsync(new
        {
            model = options.Model,
            instructions,
            input = new[] { new { role = "user", content = new object[] { new { type = "input_text", text = prompt }, new { type = "input_file", file_id = inputFileId } } } },
            tools = AgentToolDefinitions.All,
            tool_choice = "auto",
            parallel_tool_calls = false,
            include = new[] { "code_interpreter_call.outputs" },
            reasoning = new { effort = options.ReasoningEffort },
            max_output_tokens = options.MaximumOutputTokensPerTurn,
            store = true,
            metadata = new { application = "DeskBridge", workflow = "artifact-review-loop" }
        }, cancellationToken);

    public Task<AgentResponse> ContinueAsync(string instructions, string previousResponseId, IReadOnlyList<ToolOutput> outputs, AgentRunOptions options, CancellationToken cancellationToken) =>
        CreateResponseAsync(new
        {
            model = options.Model,
            instructions,
            previous_response_id = previousResponseId,
            input = outputs.Select(output => new { type = "function_call_output", call_id = output.CallId, output = output.Output }).ToArray(),
            tools = AgentToolDefinitions.All,
            tool_choice = "auto",
            parallel_tool_calls = false,
            include = new[] { "code_interpreter_call.outputs" },
            reasoning = new { effort = options.ReasoningEffort },
            max_output_tokens = options.MaximumOutputTokensPerTurn,
            store = true
        }, cancellationToken);

    public async Task DownloadFileAsync(string fileId, string destination, CancellationToken cancellationToken)
    {
        using var request = await CreateRequestAsync(HttpMethod.Get, $"files/{Uri.EscapeDataString(fileId)}/content", cancellationToken).ConfigureAwait(false);
        using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        await EnsureSuccessAsync(response, cancellationToken).ConfigureAwait(false);
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        await using var input = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        await using var output = File.Create(destination);
        await input.CopyToAsync(output, cancellationToken).ConfigureAwait(false);
    }

    private async Task<AgentResponse> CreateResponseAsync(object payload, CancellationToken cancellationToken)
    {
        using var request = await CreateRequestAsync(HttpMethod.Post, "responses", cancellationToken).ConfigureAwait(false);
        request.Content = JsonContent.Create(payload, options: DeskBridge.Core.Models.DeskBridgeJson.Options);
        using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        await EnsureSuccessAsync(response, cancellationToken).ConfigureAwait(false);
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
        return ParseResponse(document.RootElement);
    }

    private async Task<HttpRequestMessage> CreateRequestAsync(HttpMethod method, string relativePath, CancellationToken cancellationToken)
    {
        var apiKey = await keyStore.LoadAsync(cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(apiKey)) throw new InvalidOperationException("Save an OpenAI API key in DeskBridge Settings before starting the agent.");
        var request = new HttpRequestMessage(method, new Uri(BaseUri, relativePath));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        request.Headers.UserAgent.ParseAdd("DeskBridge/1.0");
        return request;
    }

    private static AgentResponse ParseResponse(JsonElement root)
    {
        var toolCalls = new List<ResponseToolCall>();
        var files = new Dictionary<string, GeneratedFileReference>(StringComparer.Ordinal);
        var text = new List<string>();
        if (root.TryGetProperty("output", out var output) && output.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in output.EnumerateArray())
            {
                var type = item.TryGetProperty("type", out var typeValue) ? typeValue.GetString() : null;
                if (type == "function_call")
                {
                    var argumentsText = item.GetProperty("arguments").GetString() ?? "{}";
                    using var arguments = JsonDocument.Parse(argumentsText);
                    toolCalls.Add(new ResponseToolCall(item.GetProperty("call_id").GetString() ?? string.Empty,
                        item.GetProperty("name").GetString() ?? string.Empty, arguments.RootElement.Clone()));
                }

                CollectOutputText(item, text);
                CollectGeneratedFiles(item, files);
            }
        }

        var usage = root.TryGetProperty("usage", out var usageValue) && usageValue.ValueKind == JsonValueKind.Object
            ? new AgentUsage(GetInt(usageValue, "input_tokens"), GetInt(usageValue, "output_tokens"), GetInt(usageValue, "total_tokens"))
            : new AgentUsage(0, 0, 0);
        var error = root.TryGetProperty("error", out var errorValue) && errorValue.ValueKind == JsonValueKind.Object &&
                    errorValue.TryGetProperty("message", out var messageValue) ? messageValue.GetString() : null;
        return new AgentResponse(root.GetProperty("id").GetString() ?? string.Empty,
            root.TryGetProperty("status", out var status) ? status.GetString() ?? "unknown" : "unknown",
            string.Join(Environment.NewLine, text), toolCalls, files.Values.ToArray(), usage, error);
    }

    private static void CollectOutputText(JsonElement element, ICollection<string> output)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            if (element.TryGetProperty("type", out var type) && type.GetString() == "output_text" &&
                element.TryGetProperty("text", out var text) && text.ValueKind == JsonValueKind.String)
                output.Add(text.GetString() ?? string.Empty);
            foreach (var property in element.EnumerateObject()) CollectOutputText(property.Value, output);
        }
        else if (element.ValueKind == JsonValueKind.Array)
            foreach (var child in element.EnumerateArray()) CollectOutputText(child, output);
    }

    private static void CollectGeneratedFiles(JsonElement element, IDictionary<string, GeneratedFileReference> files)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            if (element.TryGetProperty("file_id", out var idValue) && idValue.ValueKind == JsonValueKind.String)
            {
                var id = idValue.GetString();
                if (!string.IsNullOrWhiteSpace(id))
                {
                    var filename = element.TryGetProperty("filename", out var filenameValue) && filenameValue.ValueKind == JsonValueKind.String
                        ? filenameValue.GetString() : null;
                    files[id] = new GeneratedFileReference(id, filename);
                }
            }
            foreach (var property in element.EnumerateObject()) CollectGeneratedFiles(property.Value, files);
        }
        else if (element.ValueKind == JsonValueKind.Array)
            foreach (var child in element.EnumerateArray()) CollectGeneratedFiles(child, files);
    }

    private static int GetInt(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.TryGetInt32(out var number) ? number : 0;

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode) return;
        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (body.Length > 2_000) body = body[..2_000] + "…";
        throw new HttpRequestException($"OpenAI API returned {(int)response.StatusCode} {response.ReasonPhrase}: {body}", null, response.StatusCode);
    }
}
