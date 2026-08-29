namespace DeskBridge.Core.Models;

public sealed record DeskBridgeSettings
{
    public string? WorkspacePath { get; init; }
    public bool WorkspaceMode { get; init; } = true;
    public string ThemeMode { get; init; } = "system";
    public AgentSettings Agent { get; init; } = new();
    public Dictionary<string, bool> SkillIntegrations { get; init; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, string> Permissions { get; init; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed record AgentSettings
{
    public string Model { get; init; } = "gpt-5.6-luna";
    public string ReasoningEffort { get; init; } = "low";
    public int MaximumIterations { get; init; } = 4;
    public int MaximumToolCalls { get; init; } = 24;
    public int MaximumOutputTokensPerTurn { get; init; } = 6_000;
}

public sealed record ActivityEntry(
    DateTimeOffset Timestamp,
    string Action,
    string Target,
    string Result,
    long DurationMs);

public sealed record PermissionRequest(
    string Id,
    string Action,
    string Summary,
    string Target,
    IReadOnlyList<string> Items);

public sealed record PermissionResponse(string Id, bool Allowed);
