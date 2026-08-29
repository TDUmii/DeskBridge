using System.Text.Json;

namespace DeskBridge.Core.Agent;

public sealed record AgentRunRequest(
    string Workspace,
    string SourcePath,
    string UserRequest,
    AgentRunOptions Options);

public sealed record AgentRunOptions(
    string Model,
    string ReasoningEffort,
    int MaximumIterations,
    int MaximumToolCalls,
    int MaximumOutputTokensPerTurn);

public sealed record AgentRunResult(
    bool Success,
    string Status,
    string? BestArtifactPath,
    int? Score,
    string Summary,
    IReadOnlyList<string> RemainingIssues,
    AgentUsage Usage,
    string RunDirectory);

public sealed record AgentUsage(int InputTokens, int OutputTokens, int TotalTokens)
{
    public static AgentUsage operator +(AgentUsage left, AgentUsage right) =>
        new(left.InputTokens + right.InputTokens, left.OutputTokens + right.OutputTokens, left.TotalTokens + right.TotalTokens);
}

public sealed record AgentProgress(string Stage, string Message, int Iteration, int ToolCalls, string? Detail = null);

public sealed record ResponseToolCall(string CallId, string Name, JsonElement Arguments);

public sealed record GeneratedFileReference(string FileId, string? Filename);

public sealed record AgentResponse(
    string Id,
    string Status,
    string OutputText,
    IReadOnlyList<ResponseToolCall> ToolCalls,
    IReadOnlyList<GeneratedFileReference> GeneratedFiles,
    AgentUsage Usage,
    string? ErrorMessage);

public sealed record ToolOutput(string CallId, string Output);

public sealed record LocalArtifactInspection(
    string Path,
    string Extension,
    long Size,
    string Sha256,
    int? WordCount,
    int? LineCount,
    string? MarkdownPath,
    string Summary);
