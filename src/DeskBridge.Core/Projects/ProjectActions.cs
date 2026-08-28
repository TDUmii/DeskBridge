using System.Globalization;
using System.Text.Json;
using DeskBridge.Core.Actions;
using DeskBridge.Core.Models;

namespace DeskBridge.Core.Projects;

public sealed class CreateProjectAction : IDeskBridgeAction
{
    private static readonly HashSet<string> ProjectTypes = new(StringComparer.OrdinalIgnoreCase)
        { "static-web", "python", "generic" };

    public string Name => "create_project";

    public async Task<ActionResult> ExecuteAsync(JsonElement arguments, ActionContext context, CancellationToken cancellationToken)
    {
        var root = context.WorkspaceGuard.EnsureInside(arguments.RequiredString("rootPath"));
        var projectType = arguments.RequiredString("projectType");
        if (!ProjectTypes.Contains(projectType))
        {
            return ActionResult.Fail(ErrorCodes.InvalidRequest, "projectType must be static-web, python, or generic.");
        }

        var files = ProjectFiles.Parse(arguments, context, root);
        if (Directory.Exists(root) && Directory.EnumerateFileSystemEntries(root).Any())
        {
            return ActionResult.Fail(ErrorCodes.FileAlreadyExists, "Project folder already exists and is not empty.");
        }

        Directory.CreateDirectory(root);
        foreach (var file in files)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(file.AbsolutePath)!);
            await AtomicFile.WriteTextAsync(file.AbsolutePath, file.Content, cancellationToken).ConfigureAwait(false);
        }

        await ProjectFiles.EnsureDeskBridgeIgnoredAsync(root, cancellationToken).ConfigureAwait(false);
        return ActionResult.Ok(new { rootPath = root, projectType, files = files.Select(file => file.RelativePath).ToArray() });
    }
}

public sealed class UpdateProjectAction : IDeskBridgeAction
{
    public string Name => "update_project";

    public async Task<ActionResult> ExecuteAsync(JsonElement arguments, ActionContext context, CancellationToken cancellationToken)
    {
        var root = context.WorkspaceGuard.EnsureInside(arguments.RequiredString("rootPath"));
        if (!Directory.Exists(root))
        {
            return ActionResult.Fail(ErrorCodes.FileNotFound, "Project folder does not exist.");
        }

        var files = ProjectFiles.Parse(arguments, context, root);
        var backup = await ProjectBackupService.CreateAsync(root, files, cancellationToken).ConfigureAwait(false);
        foreach (var file in files)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(file.AbsolutePath)!);
            await AtomicFile.WriteTextAsync(file.AbsolutePath, file.Content, cancellationToken).ConfigureAwait(false);
        }

        await ProjectFiles.EnsureDeskBridgeIgnoredAsync(root, cancellationToken).ConfigureAwait(false);
        return ActionResult.Ok(new
        {
            rootPath = root,
            files = files.Select(file => file.RelativePath).ToArray(),
            backup
        });
    }
}

internal sealed record ProjectFile(string RelativePath, string AbsolutePath, string Content);

internal static class ProjectFiles
{
    public static IReadOnlyList<ProjectFile> Parse(JsonElement arguments, ActionContext context, string root)
    {
        if (!arguments.TryGetProperty("files", out var filesElement) || filesElement.ValueKind != JsonValueKind.Array ||
            filesElement.GetArrayLength() == 0)
        {
            throw new DeskBridgeActionException(ErrorCodes.InvalidRequest, "'files' must be a non-empty array.");
        }

        var files = new List<ProjectFile>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in filesElement.EnumerateArray())
        {
            var relative = item.RequiredString("path").Replace('\\', '/');
            if (!seen.Add(relative))
            {
                throw new DeskBridgeActionException(ErrorCodes.ProjectPathInvalid, $"Duplicate project path: {relative}");
            }

            var content = item.TryGetProperty("content", out var contentElement) && contentElement.ValueKind == JsonValueKind.String
                ? contentElement.GetString() ?? string.Empty
                : throw new DeskBridgeActionException(ErrorCodes.InvalidRequest, "Every project file needs string content.");
            files.Add(new ProjectFile(relative, context.WorkspaceGuard.ResolveRelative(root, relative), content));
        }

        return files;
    }

    public static async Task EnsureDeskBridgeIgnoredAsync(string root, CancellationToken cancellationToken)
    {
        var path = Path.Combine(root, ".gitignore");
        var content = File.Exists(path) ? await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false) : string.Empty;
        if (!content.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
                .Any(line => string.Equals(line.Trim(), ".deskbridge/", StringComparison.OrdinalIgnoreCase)))
        {
            var separator = content.Length == 0 || content.EndsWith('\n') ? string.Empty : Environment.NewLine;
            await AtomicFile.WriteTextAsync(path, content + separator + ".deskbridge/" + Environment.NewLine, cancellationToken)
                .ConfigureAwait(false);
        }
    }
}

internal static class ProjectBackupService
{
    public static async Task<string?> CreateAsync(
        string root,
        IReadOnlyList<ProjectFile> files,
        CancellationToken cancellationToken)
    {
        var existing = files.Where(file => File.Exists(file.AbsolutePath)).ToArray();
        if (existing.Length == 0)
        {
            return null;
        }

        var backupsRoot = Path.Combine(root, ".deskbridge", "backups");
        var backup = Path.Combine(backupsRoot, DateTimeOffset.UtcNow.ToString("yyyyMMdd-HHmmss-fff", CultureInfo.InvariantCulture));
        foreach (var file in existing)
        {
            var destination = Path.Combine(backup, file.RelativePath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            await using var sourceStream = File.OpenRead(file.AbsolutePath);
            await using var destinationStream = File.Create(destination);
            await sourceStream.CopyToAsync(destinationStream, cancellationToken).ConfigureAwait(false);
        }

        Directory.CreateDirectory(backupsRoot);
        foreach (var old in new DirectoryInfo(backupsRoot).EnumerateDirectories()
                     .OrderByDescending(directory => directory.Name).Skip(10))
        {
            old.Delete(true);
        }

        return backup;
    }
}
