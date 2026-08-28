using System.Text.Json;
using DeskBridge.Core.Actions;
using DeskBridge.Core.Models;
using SixLabors.ImageSharp;

namespace DeskBridge.Core.Downloads;

public sealed class DownloadAssetAction(SecureImageDownloader downloader) : IDeskBridgeAction
{
    public string Name => "download_asset";

    public async Task<ActionResult> ExecuteAsync(JsonElement arguments, ActionContext context, CancellationToken cancellationToken)
    {
        var url = arguments.RequiredString("url");
        var destination = context.WorkspaceGuard.EnsureInside(arguments.RequiredString("destination"), false);
        var downloaded = await downloader.DownloadAsync(url, cancellationToken).ConfigureAwait(false);
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        var temporary = destination + ".deskbridge-tmp";
        await File.WriteAllBytesAsync(temporary, downloaded.Bytes, cancellationToken).ConfigureAwait(false);
        File.Move(temporary, destination, true);
        await SourceMetadata.AppendAsync(context.WorkspaceGuard.WorkspaceRoot, destination, downloaded, cancellationToken)
            .ConfigureAwait(false);
        return ActionResult.Ok(new
        {
            path = destination,
            bytes = downloaded.Bytes.Length,
            contentType = downloaded.ContentType,
            source = downloaded.FinalUri.ToString()
        });
    }
}

public sealed class ImportAssetAction : IDeskBridgeAction
{
    public string Name => "import_asset";

    public async Task<ActionResult> ExecuteAsync(JsonElement arguments, ActionContext context, CancellationToken cancellationToken)
    {
        var source = Path.GetFullPath(arguments.RequiredString("source"));
        var destination = context.WorkspaceGuard.EnsureInside(arguments.RequiredString("destination"), false);
        if (!File.Exists(source))
        {
            return ActionResult.Fail(ErrorCodes.FileNotFound, "Source asset does not exist.");
        }

        var bytes = await File.ReadAllBytesAsync(source, cancellationToken).ConfigureAwait(false);
        var detected = ImagePayloadValidator.Detect(bytes);
        if (detected is null)
        {
            return ActionResult.Fail(ErrorCodes.UnsupportedImageFormat, "Source is not a supported JPG, PNG, WebP, GIF, or safe SVG image.");
        }

        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        await File.WriteAllBytesAsync(destination + ".deskbridge-tmp", bytes, cancellationToken).ConfigureAwait(false);
        File.Move(destination + ".deskbridge-tmp", destination, true);
        return ActionResult.Ok(new { source, destination, bytes = bytes.Length, format = detected });
    }
}

internal static class SourceMetadata
{
    public static async Task AppendAsync(
        string workspaceRoot,
        string destination,
        DownloadedImage downloaded,
        CancellationToken cancellationToken)
    {
        var relative = Path.GetRelativePath(workspaceRoot, destination).Replace('\\', '/');
        var segments = relative.Split('/');
        var assetsIndex = Array.FindIndex(segments, value => string.Equals(value, "assets", StringComparison.OrdinalIgnoreCase));
        if (assetsIndex < 0)
        {
            return;
        }

        var assetsRoot = Path.Combine(workspaceRoot, Path.Combine(segments.Take(assetsIndex + 1).ToArray()));
        var metadataPath = Path.Combine(assetsRoot, "sources.json");
        List<Dictionary<string, object?>> records;
        try
        {
            records = File.Exists(metadataPath)
                ? JsonSerializer.Deserialize<List<Dictionary<string, object?>>>(
                    await File.ReadAllTextAsync(metadataPath, cancellationToken).ConfigureAwait(false), DeskBridgeJson.Options) ?? []
                : [];
        }
        catch (JsonException)
        {
            records = [];
        }

        records.RemoveAll(record => string.Equals(record.GetValueOrDefault("file")?.ToString(),
            string.Join('/', segments.Skip(assetsIndex + 1)), StringComparison.OrdinalIgnoreCase));
        records.Add(new Dictionary<string, object?>
        {
            ["file"] = string.Join('/', segments.Skip(assetsIndex + 1)),
            ["source"] = downloaded.FinalUri.ToString(),
            ["downloadedAt"] = DateTimeOffset.UtcNow,
            ["contentType"] = downloaded.ContentType
        });
        Directory.CreateDirectory(assetsRoot);
        await File.WriteAllTextAsync(metadataPath,
            JsonSerializer.Serialize(records, new JsonSerializerOptions(DeskBridgeJson.Options) { WriteIndented = true }),
            cancellationToken).ConfigureAwait(false);
    }
}
