using System.Text.Json;
using DeskBridge.Core.Models;

namespace DeskBridge.Core.Agent;

public sealed class WebAgentNativeController(BrowserAgentStore store)
{
    public static IReadOnlySet<string> Actions { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "web_agent_claim", "web_agent_source_chunk", "web_agent_progress", "web_agent_context", "web_agent_candidate", "web_agent_fail"
    };

    public async Task<ActionResult> ExecuteAsync(ActionRequest request, CancellationToken cancellationToken)
    {
        if (request.Version != 1 || request.Arguments.ValueKind != JsonValueKind.Object)
            return ActionResult.Fail(ErrorCodes.InvalidRequest, "Invalid web-agent request.");
        try
        {
            return request.Action switch
            {
                "web_agent_claim" => ActionResult.Ok(store.ClaimNext()),
                "web_agent_source_chunk" => ActionResult.Ok(store.ReadSourceChunk(
                    String(request.Arguments, "runId"), Long(request.Arguments, "offset"), Integer(request.Arguments, "maxBytes"))),
                "web_agent_progress" => ActionResult.Ok(store.UpdateProgress(
                    String(request.Arguments, "runId"), String(request.Arguments, "stage"), String(request.Arguments, "message"), OptionalString(request.Arguments, "chatUrl"))),
                "web_agent_context" => ActionResult.Ok(await store.ReadWorkspaceContextAsync(
                    String(request.Arguments, "runId"), ContextEnvelope(request.Arguments), cancellationToken).ConfigureAwait(false)),
                "web_agent_candidate" => ActionResult.Ok(await store.InspectCandidateAsync(
                    String(request.Arguments, "runId"), String(request.Arguments, "downloadedPath"), Assessment(request.Arguments), cancellationToken).ConfigureAwait(false)),
                "web_agent_fail" => ActionResult.Ok(store.Fail(String(request.Arguments, "runId"), String(request.Arguments, "message"))),
                _ => ActionResult.Fail(ErrorCodes.UnknownAction, "Unknown web-agent action.")
            };
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return ActionResult.Fail(ErrorCodes.ExecutionFailed, exception.Message);
        }
    }

    private static BrowserCandidateAssessment Assessment(JsonElement arguments)
    {
        if (!arguments.TryGetProperty("assessment", out var value) || value.ValueKind != JsonValueKind.Object)
            throw new ArgumentException("assessment must be an object.");
        var assessment = JsonSerializer.Deserialize<BrowserCandidateAssessment>(value.GetRawText(), DeskBridgeJson.Options)
            ?? throw new ArgumentException("assessment is invalid.");
        if (string.IsNullOrWhiteSpace(assessment.CandidateFile) || string.IsNullOrWhiteSpace(assessment.Summary) ||
            assessment.RequirementsMet is null || assessment.RemainingIssues is null ||
            assessment.RequirementsMet.Any(string.IsNullOrWhiteSpace) || assessment.RemainingIssues.Any(string.IsNullOrWhiteSpace))
            throw new ArgumentException("assessment fields must be non-empty strings and arrays of non-empty strings.");
        return assessment;
    }

    private static BrowserContextEnvelope ContextEnvelope(JsonElement arguments)
    {
        if (!arguments.TryGetProperty("requests", out var value) || value.ValueKind != JsonValueKind.Array)
            throw new ArgumentException("requests must be an array.");
        var requests = JsonSerializer.Deserialize<IReadOnlyList<BrowserContextRequest>>(value.GetRawText(), DeskBridgeJson.Options)
            ?? throw new ArgumentException("requests are invalid.");
        if (requests.Any(request => string.IsNullOrWhiteSpace(request.Action))) throw new ArgumentException("Every context request requires an action.");
        return new BrowserContextEnvelope(requests);
    }

    private static string String(JsonElement arguments, string name)
    {
        if (!arguments.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(value.GetString()))
            throw new ArgumentException($"{name} must be a non-empty string.");
        return value.GetString()!;
    }

    private static string? OptionalString(JsonElement arguments, string name) =>
        arguments.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
    private static int Integer(JsonElement arguments, string name) =>
        arguments.TryGetProperty(name, out var value) && value.TryGetInt32(out var number) ? number : throw new ArgumentException($"{name} must be an integer.");
    private static long Long(JsonElement arguments, string name) =>
        arguments.TryGetProperty(name, out var value) && value.TryGetInt64(out var number) ? number : throw new ArgumentException($"{name} must be an integer.");
}
