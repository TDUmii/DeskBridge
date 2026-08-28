using System.Text.Json;
using DeskBridge.Core.Actions;
using DeskBridge.Core.Models;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using ImageSharpImage = SixLabors.ImageSharp.Image;
using ImageSharpSize = SixLabors.ImageSharp.Size;

namespace DeskBridge.Core.Images;

public sealed class InspectImageAction : IDeskBridgeAction
{
    public string Name => "inspect_image";

    public async Task<ActionResult> ExecuteAsync(JsonElement arguments, ActionContext context, CancellationToken cancellationToken)
    {
        var path = context.WorkspaceGuard.EnsureInside(arguments.RequiredString("path"), false);
        if (!File.Exists(path))
        {
            return ActionResult.Fail(ErrorCodes.FileNotFound, "Image does not exist.");
        }

        try
        {
            using var image = await ImageSharpImage.LoadAsync<Rgba32>(path, cancellationToken).ConfigureAwait(false);
            var hasAlpha = false;
            image.ProcessPixelRows(accessor =>
            {
                for (var y = 0; y < accessor.Height && !hasAlpha; y++)
                {
                    foreach (var pixel in accessor.GetRowSpan(y))
                    {
                        if (pixel.A == byte.MaxValue) continue;
                        hasAlpha = true;
                        break;
                    }
                }
            });
            return ActionResult.Ok(new
            {
                width = image.Width,
                height = image.Height,
                format = image.Metadata.DecodedImageFormat?.Name.ToLowerInvariant() ?? "unknown",
                bytes = new FileInfo(path).Length,
                hasAlpha
            });
        }
        catch (UnknownImageFormatException)
        {
            return ActionResult.Fail(ErrorCodes.UnsupportedImageFormat, "Image format is not supported.");
        }
    }
}

public sealed class ResizeImageAction : IDeskBridgeAction
{
    public string Name => "resize_image";

    public Task<ActionResult> ExecuteAsync(JsonElement arguments, ActionContext context, CancellationToken cancellationToken) =>
        ImageProcessor.ProcessAsync(arguments, context, ImageOperation.Resize, cancellationToken);
}

public sealed class CompressImageAction : IDeskBridgeAction
{
    public string Name => "compress_image";

    public Task<ActionResult> ExecuteAsync(JsonElement arguments, ActionContext context, CancellationToken cancellationToken) =>
        ImageProcessor.ProcessAsync(arguments, context, ImageOperation.Compress, cancellationToken);
}

public sealed class ConvertImageAction : IDeskBridgeAction
{
    public string Name => "convert_image";

    public Task<ActionResult> ExecuteAsync(JsonElement arguments, ActionContext context, CancellationToken cancellationToken) =>
        ImageProcessor.ProcessAsync(arguments, context, ImageOperation.Convert, cancellationToken);
}

internal enum ImageOperation { Resize, Compress, Convert }

internal static class ImageProcessor
{
    public static async Task<ActionResult> ProcessAsync(
        JsonElement arguments,
        ActionContext context,
        ImageOperation operation,
        CancellationToken cancellationToken)
    {
        var source = context.WorkspaceGuard.EnsureInside(arguments.RequiredString("source"), false);
        var destination = context.WorkspaceGuard.EnsureInside(arguments.RequiredString("destination"), false);
        if (!File.Exists(source))
        {
            return ActionResult.Fail(ErrorCodes.FileNotFound, "Source image does not exist.");
        }

        if (string.Equals(source, destination, StringComparison.OrdinalIgnoreCase))
        {
            return ActionResult.Fail(ErrorCodes.ImageProcessingFailed, "Destination must not overwrite the source image.");
        }

        var quality = arguments.OptionalInt("quality") ?? 82;
        if (quality is < 1 or > 100)
        {
            return ActionResult.Fail(ErrorCodes.InvalidRequest, "quality must be between 1 and 100.");
        }

        try
        {
            using var image = await ImageSharpImage.LoadAsync(source, cancellationToken).ConfigureAwait(false);
            if (operation == ImageOperation.Resize)
            {
                var requestedWidth = arguments.OptionalInt("width");
                var requestedHeight = arguments.OptionalInt("height");
                if (requestedWidth is null && requestedHeight is null || requestedWidth is <= 0 || requestedHeight is <= 0)
                {
                    return ActionResult.Fail(ErrorCodes.InvalidRequest, "A positive width or height is required.");
                }

                var keepAspect = arguments.OptionalBool("keepAspectRatio", true);
                var allowUpscale = arguments.OptionalBool("allowUpscale", false);
                var target = CalculateSize(image.Width, image.Height, requestedWidth, requestedHeight, keepAspect);
                if (!allowUpscale && (target.Width > image.Width || target.Height > image.Height))
                {
                    target = (image.Width, image.Height);
                }

                image.Mutate(value => value.Resize(new ResizeOptions
                {
                    Size = new ImageSharpSize(target.Width, target.Height),
                    Mode = keepAspect ? ResizeMode.Max : ResizeMode.Stretch,
                    Sampler = KnownResamplers.Lanczos3
                }));
            }

            var format = operation == ImageOperation.Convert
                ? arguments.RequiredString("format")
                : Path.GetExtension(destination).TrimStart('.');
            var encoder = CreateEncoder(format, quality);
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            var temporary = destination + ".deskbridge-tmp";
            await image.SaveAsync(temporary, encoder, cancellationToken).ConfigureAwait(false);
            File.Move(temporary, destination, true);
            return ActionResult.Ok(new
            {
                source,
                destination,
                width = image.Width,
                height = image.Height,
                format = NormalizeFormat(format),
                bytes = new FileInfo(destination).Length
            });
        }
        catch (UnknownImageFormatException)
        {
            return ActionResult.Fail(ErrorCodes.UnsupportedImageFormat, "Source image format is unsupported.");
        }
        catch (Exception exception) when (exception is IOException or NotSupportedException or ArgumentException)
        {
            return ActionResult.Fail(ErrorCodes.ImageProcessingFailed, exception.Message);
        }
    }

    private static (int Width, int Height) CalculateSize(int sourceWidth, int sourceHeight, int? width, int? height, bool keepAspect)
    {
        if (!keepAspect)
        {
            return (width ?? sourceWidth, height ?? sourceHeight);
        }

        if (width is not null && height is not null)
        {
            var scale = Math.Min((double)width.Value / sourceWidth, (double)height.Value / sourceHeight);
            return ((int)Math.Round(sourceWidth * scale), (int)Math.Round(sourceHeight * scale));
        }

        if (width is not null)
        {
            return (width.Value, (int)Math.Round(sourceHeight * ((double)width.Value / sourceWidth)));
        }

        return ((int)Math.Round(sourceWidth * ((double)height!.Value / sourceHeight)), height.Value);
    }

    private static IImageEncoder CreateEncoder(string format, int quality) => NormalizeFormat(format) switch
    {
        "jpg" => new JpegEncoder { Quality = quality },
        "png" => new PngEncoder { CompressionLevel = PngCompressionLevel.BestCompression },
        "webp" => new WebpEncoder { Quality = quality },
        _ => throw new DeskBridgeActionException(ErrorCodes.UnsupportedImageFormat, "Output format must be png, jpg/jpeg, or webp.")
    };

    private static string NormalizeFormat(string format) => format.Trim().TrimStart('.').ToLowerInvariant() switch
    {
        "jpeg" => "jpg",
        var value => value
    };
}
