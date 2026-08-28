using DeskBridge.Core.Actions;
using DeskBridge.Core.Models;

namespace DeskBridge.Core.Security;

public sealed class WorkspaceGuard
{
    private readonly string _workspaceRoot;
    private readonly string _workspacePrefix;

    public WorkspaceGuard(string workspaceRoot)
    {
        if (string.IsNullOrWhiteSpace(workspaceRoot))
        {
            throw new ArgumentException("A workspace path is required.", nameof(workspaceRoot));
        }

        _workspaceRoot = Normalize(workspaceRoot).TrimEnd(Path.DirectorySeparatorChar);
        _workspacePrefix = _workspaceRoot + Path.DirectorySeparatorChar;
    }

    public string WorkspaceRoot => _workspaceRoot;

    public string EnsureInside(string path, bool allowWorkspaceRoot = true)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new DeskBridgeActionException(ErrorCodes.WorkspaceViolation, "A path is required.");
        }

        var normalized = Normalize(path);
        var isRoot = string.Equals(normalized.TrimEnd(Path.DirectorySeparatorChar), _workspaceRoot,
            StringComparison.OrdinalIgnoreCase);
        if ((!allowWorkspaceRoot || !isRoot) && !normalized.StartsWith(_workspacePrefix, StringComparison.OrdinalIgnoreCase))
        {
            throw new DeskBridgeActionException(ErrorCodes.WorkspaceViolation,
                "The target is outside the allowed workspace.");
        }

        EnsureNoReparseEscape(normalized);
        return normalized;
    }

    public string ResolveRelative(string rootPath, string childPath)
    {
        var root = EnsureInside(rootPath);
        if (string.IsNullOrWhiteSpace(childPath) || Path.IsPathRooted(childPath))
        {
            throw new DeskBridgeActionException(ErrorCodes.ProjectPathInvalid,
                "Project child paths must be non-empty relative paths.");
        }

        var normalizedChild = childPath.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
        if (normalizedChild.Split(Path.DirectorySeparatorChar).Any(part => part is ".." or "."))
        {
            throw new DeskBridgeActionException(ErrorCodes.ProjectPathInvalid,
                "Project child paths cannot contain traversal segments.");
        }

        var resolved = Path.GetFullPath(Path.Combine(root, normalizedChild));
        var rootPrefix = root.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!resolved.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase))
        {
            throw new DeskBridgeActionException(ErrorCodes.ProjectPathInvalid,
                "Project child path escapes the project root.");
        }

        return EnsureInside(resolved, false);
    }

    private static string Normalize(string path) => Path.GetFullPath(path)
        .Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);

    private void EnsureNoReparseEscape(string target)
    {
        var current = Directory.Exists(target) ? target : Path.GetDirectoryName(target);
        while (!string.IsNullOrEmpty(current) &&
               current.StartsWith(_workspaceRoot, StringComparison.OrdinalIgnoreCase))
        {
            if (Directory.Exists(current) &&
                (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
            {
                var resolved = new DirectoryInfo(current).ResolveLinkTarget(true)?.FullName;
                if (resolved is not null)
                {
                    var normalized = Normalize(resolved);
                    var isRoot = string.Equals(normalized.TrimEnd(Path.DirectorySeparatorChar), _workspaceRoot,
                        StringComparison.OrdinalIgnoreCase);
                    if (!isRoot && !normalized.StartsWith(_workspacePrefix, StringComparison.OrdinalIgnoreCase))
                    {
                        throw new DeskBridgeActionException(ErrorCodes.WorkspaceViolation,
                            "A reparse point would escape the allowed workspace.");
                    }
                }
            }

            current = Path.GetDirectoryName(current);
        }
    }
}
