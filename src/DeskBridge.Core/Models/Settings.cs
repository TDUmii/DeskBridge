namespace DeskBridge.Core.Models;

public sealed record DeskBridgeSettings
{
    public string? WorkspacePath { get; init; }
    public bool WorkspaceMode { get; init; } = true;
    public Dictionary<string, string> Permissions { get; init; } = new(StringComparer.OrdinalIgnoreCase);
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
