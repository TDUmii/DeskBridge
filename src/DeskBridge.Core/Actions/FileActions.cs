using System.Text;
using System.Text.Json;
using DeskBridge.Core.Models;

namespace DeskBridge.Core.Actions;

public sealed class ReadFileAction : IDeskBridgeAction
{
    public string Name => "read_file";

    public async Task<ActionResult> ExecuteAsync(JsonElement arguments, ActionContext context, CancellationToken cancellationToken)
    {
        var path = context.WorkspaceGuard.EnsureInside(arguments.RequiredString("path"), false);
        if (!File.Exists(path))
        {
            return ActionResult.Fail(ErrorCodes.FileNotFound, "File does not exist.");
        }

        var bytes = await File.ReadAllBytesAsync(path, cancellationToken).ConfigureAwait(false);
        var content = new UTF8Encoding(false, true).GetString(bytes);
        return ActionResult.Ok(new { content, size = bytes.LongLength, encoding = "utf-8" });
    }
}

public sealed class WriteFileAction : IDeskBridgeAction
{
    public string Name => "write_file";

    public async Task<ActionResult> ExecuteAsync(JsonElement arguments, ActionContext context, CancellationToken cancellationToken)
    {
        var path = context.WorkspaceGuard.EnsureInside(arguments.RequiredString("path"), false);
        var content = arguments.TryGetProperty("content", out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty
            : throw new DeskBridgeActionException(ErrorCodes.InvalidRequest, "'content' must be a string.");
        var overwrite = arguments.OptionalBool("overwrite", false);
        if (File.Exists(path) && !overwrite)
        {
            return ActionResult.Fail(ErrorCodes.FileAlreadyExists, "File already exists and overwrite is false.");
        }

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await AtomicFile.WriteTextAsync(path, content, cancellationToken).ConfigureAwait(false);
        return ActionResult.Ok(new { path, size = Encoding.UTF8.GetByteCount(content) });
    }
}

public sealed class CreateFileAction : IDeskBridgeAction
{
    public string Name => "create_file";

    public async Task<ActionResult> ExecuteAsync(JsonElement arguments, ActionContext context, CancellationToken cancellationToken)
    {
        var path = context.WorkspaceGuard.EnsureInside(arguments.RequiredString("path"), false);
        if (File.Exists(path))
        {
            return ActionResult.Fail(ErrorCodes.FileAlreadyExists, "File already exists.");
        }

        var content = arguments.TryGetProperty("content", out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty : string.Empty;
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await AtomicFile.WriteTextAsync(path, content, cancellationToken).ConfigureAwait(false);
        return ActionResult.Ok(new { path });
    }
}

public sealed class CreateFolderAction : IDeskBridgeAction
{
    public string Name => "create_folder";

    public Task<ActionResult> ExecuteAsync(JsonElement arguments, ActionContext context, CancellationToken cancellationToken)
    {
        var path = context.WorkspaceGuard.EnsureInside(arguments.RequiredString("path"));
        Directory.CreateDirectory(path);
        return Task.FromResult(ActionResult.Ok(new { path }));
    }
}

public sealed class ListFolderAction : IDeskBridgeAction
{
    public string Name => "list_folder";

    public Task<ActionResult> ExecuteAsync(JsonElement arguments, ActionContext context, CancellationToken cancellationToken)
    {
        var path = context.WorkspaceGuard.EnsureInside(arguments.RequiredString("path"));
        if (!Directory.Exists(path))
        {
            return Task.FromResult(ActionResult.Fail(ErrorCodes.FileNotFound, "Folder does not exist."));
        }

        var entries = new DirectoryInfo(path).EnumerateFileSystemInfos()
            .OrderBy(info => info is FileInfo ? 1 : 0).ThenBy(info => info.Name, StringComparer.OrdinalIgnoreCase)
            .Select(info => new
            {
                name = info.Name,
                type = info is DirectoryInfo ? "folder" : "file",
                size = info is FileInfo file ? file.Length : 0,
                lastModified = info.LastWriteTimeUtc
            }).ToArray();
        return Task.FromResult(ActionResult.Ok(new { entries }));
    }
}

public sealed class PatchFileAction : IDeskBridgeAction
{
    public string Name => "patch_file";

    public async Task<ActionResult> ExecuteAsync(JsonElement arguments, ActionContext context, CancellationToken cancellationToken)
    {
        var path = context.WorkspaceGuard.EnsureInside(arguments.RequiredString("path"), false);
        if (!File.Exists(path))
        {
            return ActionResult.Fail(ErrorCodes.FileNotFound, "File does not exist.");
        }

        if (!arguments.TryGetProperty("replacements", out var replacements) ||
            replacements.ValueKind != JsonValueKind.Array || replacements.GetArrayLength() == 0)
        {
            return ActionResult.Fail(ErrorCodes.InvalidRequest, "'replacements' must be a non-empty array.");
        }

        var content = await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);
        foreach (var replacement in replacements.EnumerateArray())
        {
            var oldText = replacement.RequiredString("oldText");
            var newText = replacement.TryGetProperty("newText", out var newValue) && newValue.ValueKind == JsonValueKind.String
                ? newValue.GetString() ?? string.Empty
                : throw new DeskBridgeActionException(ErrorCodes.InvalidRequest, "'newText' must be a string.");
            var first = content.IndexOf(oldText, StringComparison.Ordinal);
            if (first < 0)
            {
                return ActionResult.Fail(ErrorCodes.PatchTargetNotFound, "Patch target was not found.");
            }

            if (content.IndexOf(oldText, first + oldText.Length, StringComparison.Ordinal) >= 0)
            {
                return ActionResult.Fail(ErrorCodes.PatchTargetNotUnique, "Patch target occurs more than once.");
            }

            content = string.Concat(content.AsSpan(0, first), newText, content.AsSpan(first + oldText.Length));
        }

        await AtomicFile.WriteTextAsync(path, content, cancellationToken).ConfigureAwait(false);
        return ActionResult.Ok(new { path, replacements = replacements.GetArrayLength() });
    }
}

internal static class AtomicFile
{
    public static async Task WriteTextAsync(string path, string content, CancellationToken cancellationToken)
    {
        var temporary = path + ".deskbridge-tmp";
        await File.WriteAllTextAsync(temporary, content, new UTF8Encoding(false), cancellationToken).ConfigureAwait(false);
        File.Move(temporary, path, true);
    }
}
