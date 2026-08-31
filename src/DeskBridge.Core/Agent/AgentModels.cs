namespace DeskBridge.Core.Agent;

public enum AgentRunMode
{
    ImproveFile = 0,
    CreateNew = 1,
    WorkspaceContext = 2
}

public sealed record AgentRunRequest(
    string Workspace,
    string? SourcePath,
    string UserRequest,
    AgentRunOptions Options,
    AgentRunMode Mode = AgentRunMode.ImproveFile);

public sealed record AgentRunOptions(int MaximumIterations = 4, int PollIntervalMilliseconds = 600);

public sealed record AgentRunResult(
    bool Success,
    string Status,
    string? BestArtifactPath,
    int? Score,
    string Summary,
    IReadOnlyList<string> RemainingIssues,
    string RunDirectory,
    string? ChatUrl);

public sealed record AgentProgress(string Stage, string Message, int Iteration, string? Detail = null);

public sealed record BrowserCandidateAssessment(
    string CandidateFile,
    int Score,
    string Summary,
    IReadOnlyList<string> RequirementsMet,
    IReadOnlyList<string> RemainingIssues);

public sealed record BrowserContextRequest(
    string Action,
    string? Path = null,
    string? Query = null,
    int? StartLine = null,
    int? EndLine = null,
    int? Depth = null);

public sealed record BrowserContextEnvelope(IReadOnlyList<BrowserContextRequest> Requests);

public sealed record BrowserCandidateResult(
    bool Accepted,
    bool Terminal,
    string Status,
    string LocalPath,
    string Summary,
    int Score,
    IReadOnlyList<string> RemainingIssues,
    string? FollowUpPrompt);

public sealed record LocalArtifactInspection(
    string Path,
    string Extension,
    long Size,
    string Sha256,
    int? Words,
    int? Lines,
    string? MarkdownPath,
    string Summary);

public sealed record BrowserAgentJob
{
    public string Id { get; init; } = string.Empty;
    public string Workspace { get; init; } = string.Empty;
    public AgentRunMode Mode { get; init; }
    public string? SourcePath { get; init; }
    public string? PreservedSourcePath { get; init; }
    public string? SourceFileName { get; init; }
    public long SourceSize { get; init; }
    public bool HasSource => !string.IsNullOrWhiteSpace(PreservedSourcePath);
    public string UserRequest { get; init; } = string.Empty;
    public int MaximumIterations { get; init; } = 4;
    public int Iteration { get; init; }
    public int ContextRound { get; init; }
    public string Status { get; init; } = "pending";
    public string Stage { get; init; } = "Queued";
    public string Message { get; init; } = "Waiting for ChatGPT Web.";
    public string RequiredModel { get; init; } = "GPT-5.6 Sol";
    public string RequiredReasoning { get; init; } = "High";
    public string CandidateToken { get; init; } = string.Empty;
    public string? ChatUrl { get; init; }
    public string? BestArtifactPath { get; init; }
    public int? Score { get; init; }
    public string Summary { get; init; } = string.Empty;
    public IReadOnlyList<string> RemainingIssues { get; init; } = [];
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset UpdatedAt { get; init; }
    public string RunDirectory { get; init; } = string.Empty;
}

public sealed record BrowserAgentClaim(
    string RunId,
    AgentRunMode Mode,
    bool HasSource,
    string? SourceFileName,
    long SourceSize,
    string Prompt,
    string RequiredModel,
    string RequiredReasoning,
    int MaximumIterations,
    string CandidateToken,
    int MaximumContextRounds = 6);
