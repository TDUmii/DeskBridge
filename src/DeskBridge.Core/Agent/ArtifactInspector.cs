using System.Security.Cryptography;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using DeskBridge.Core.Skills;
using SixLabors.ImageSharp;

namespace DeskBridge.Core.Agent;

public sealed partial class ArtifactInspector(IDocumentConverter? converter = null)
{
    private static readonly HashSet<string> TextExtensions = new(StringComparer.OrdinalIgnoreCase)
        { ".txt", ".md", ".csv", ".json", ".xml", ".html", ".htm", ".css", ".js", ".ts", ".tsx", ".jsx", ".cs", ".py", ".cpp", ".h", ".yml", ".yaml" };
    private static readonly HashSet<string> DocumentExtensions = new(StringComparer.OrdinalIgnoreCase)
        { ".doc", ".docx", ".docm", ".odt", ".rtf", ".epub", ".pdf", ".ppt", ".pps", ".pot", ".pptx", ".pptm", ".ppsx", ".ppsm", ".odp", ".xls", ".xlsx", ".xlsm", ".xlsb", ".ods" };
    private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
        { ".png", ".jpg", ".jpeg", ".webp", ".gif", ".bmp" };
    private static readonly HashSet<string> ArchiveExtensions = new(StringComparer.OrdinalIgnoreCase) { ".zip" };
    private readonly IDocumentConverter _converter = converter ?? new AnyDocDocumentConverter();

    public async Task<LocalArtifactInspection> InspectAsync(string path, string inspectionDirectory, CancellationToken cancellationToken)
    {
        if (!File.Exists(path)) throw new FileNotFoundException("Artifact does not exist.", path);
        var extension = Path.GetExtension(path).ToLowerInvariant();
        var info = new FileInfo(path);
        var hash = await ComputeHashAsync(path, cancellationToken).ConfigureAwait(false);
        int? words = null;
        int? lines = null;
        string? markdownPath = null;
        var summary = $"{info.Name}: {info.Length} bytes, {extension} file.";

        if (TextExtensions.Contains(extension))
        {
            var text = await ReadBoundedTextAsync(path, 100_000, cancellationToken).ConfigureAwait(false);
            words = WordPattern().Matches(text).Count;
            lines = text.Length == 0 ? 0 : text.Count(character => character == '\n') + 1;
            summary = $"Text artifact with {words} words and {lines} lines. Preview: {Truncate(Collapse(text), 1_500)}";
        }
        else if (DocumentExtensions.Contains(extension))
        {
            Directory.CreateDirectory(inspectionDirectory);
            markdownPath = Path.Combine(inspectionDirectory, Path.GetFileNameWithoutExtension(path) + "-extracted.md");
            var conversion = await _converter.ConvertAsync(path, markdownPath, cancellationToken).ConfigureAwait(false);
            if (conversion.ExitCode == 0 && File.Exists(markdownPath))
            {
                var markdown = await ReadBoundedTextAsync(markdownPath, 100_000, cancellationToken).ConfigureAwait(false);
                words = WordPattern().Matches(markdown).Count;
                lines = markdown.Length == 0 ? 0 : markdown.Count(character => character == '\n') + 1;
                summary = $"Document extracted to Markdown with {words} words and {lines} lines. Preview: {Truncate(Collapse(markdown), 1_500)}";
            }
            else if (conversion.ExitCode == 3)
                summary = "Document is readable as a file but its pages require OCR; hosted OCR was not used.";
            else
                summary = $"Document conversion failed: {Truncate(conversion.StandardError, 1_000)}";
        }
        else if (ImageExtensions.Contains(extension))
        {
            var image = await SixLabors.ImageSharp.Image.IdentifyAsync(path, cancellationToken).ConfigureAwait(false);
            summary = image is null ? "Image metadata could not be decoded." : $"Image artifact: {image.Width} × {image.Height}px, {image.PixelType.BitsPerPixel} bits per pixel.";
        }
        else if (ArchiveExtensions.Contains(extension))
        {
            summary = InspectZip(path);
        }

        return new LocalArtifactInspection(path, extension, info.Length, hash, words, lines, markdownPath, summary);
    }

    private static async Task<string> ComputeHashAsync(string path, CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(path);
        var hash = await SHA256.HashDataAsync(stream, cancellationToken).ConfigureAwait(false);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static async Task<string> ReadBoundedTextAsync(string path, int maximumCharacters, CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(path, Encoding.UTF8, true);
        var buffer = new char[maximumCharacters];
        var read = await reader.ReadBlockAsync(buffer.AsMemory(), cancellationToken).ConfigureAwait(false);
        return new string(buffer, 0, read);
    }

    private static string InspectZip(string path)
    {
        using var archive = ZipFile.OpenRead(path);
        if (archive.Entries.Count == 0) throw new InvalidDataException("The downloaded ZIP is empty.");
        if (archive.Entries.Count > 5_000) throw new InvalidDataException("The downloaded ZIP contains too many entries to verify safely.");

        long totalUncompressed = 0;
        var files = new List<string>();
        foreach (var entry in archive.Entries)
        {
            var normalized = entry.FullName.Replace('\\', '/');
            var segments = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (string.IsNullOrWhiteSpace(normalized) || normalized.StartsWith('/') || Path.IsPathRooted(normalized) || segments.Any(segment => segment == ".."))
                throw new InvalidDataException("The downloaded ZIP contains an unsafe file path.");
            totalUncompressed = checked(totalUncompressed + entry.Length);
            if (totalUncompressed > 2L * 1024 * 1024 * 1024)
                throw new InvalidDataException("The downloaded ZIP expands beyond the 2 GB verification limit.");
            if (!normalized.EndsWith('/')) files.Add(normalized);
        }
        if (files.Count == 0) throw new InvalidDataException("The downloaded ZIP contains no files.");
        var compressed = Math.Max(1L, new FileInfo(path).Length);
        if (totalUncompressed > 10L * 1024 * 1024 && totalUncompressed / compressed > 500)
            throw new InvalidDataException("The downloaded ZIP has an unsafe compression ratio.");

        var preview = string.Join(", ", files.Take(30));
        if (files.Count > 30) preview += $", and {files.Count - 30} more";
        return $"ZIP artifact with {files.Count} files and {totalUncompressed} uncompressed bytes. Entries: {preview}";
    }

    private static string Collapse(string value) => WhitespacePattern().Replace(value, " ").Trim();
    private static string Truncate(string value, int maximum) => value.Length <= maximum ? value : value[..maximum] + "…";
    [GeneratedRegex(@"[\p{L}\p{N}_'-]+", RegexOptions.CultureInvariant)] private static partial Regex WordPattern();
    [GeneratedRegex(@"\s+", RegexOptions.CultureInvariant)] private static partial Regex WhitespacePattern();
}
