namespace DeskBridge.Core.Models;

public sealed record DeskBridgeSettings
{
    public string? WorkspacePath { get; init; }
    public bool WorkspaceMode { get; init; } = true;
    public string ThemeMode { get; init; } = "system";
    public string LanguageMode { get; init; } = "en";
    public AgentSettings Agent { get; init; } = new();
    public Dictionary<string, bool> SkillIntegrations { get; init; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, string> Permissions { get; init; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed record AgentSettings
{
    public int MaximumIterations { get; init; } = 4;
    public string Transport { get; init; } = "chatgpt-web-only";
    public string RequiredModel { get; init; } = "GPT-5.6 Sol";
    public string RequiredReasoning { get; init; } = "High";
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
