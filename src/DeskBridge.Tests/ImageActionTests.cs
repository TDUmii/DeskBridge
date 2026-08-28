using System.Text.Json;
using DeskBridge.Core.Images;
using DeskBridge.Core.Models;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace DeskBridge.Tests;

public sealed class ImageActionTests
{
    [Fact]
    public async Task ConvertsPngToWebpAndResizesWithoutUpscale()
    {
        using var workspace = new TestWorkspace();
        var source = workspace.PathOf("source.png");
        using (var image = new Image<Rgba32>(100, 50, new Rgba32(20, 80, 120, 200))) await image.SaveAsPngAsync(source);
        var converted = workspace.PathOf("converted.webp");
        using (var json = JsonDocument.Parse(JsonSerializer.Serialize(new { source, destination = converted, format = "webp", quality = 80 })))
            Assert.True((await new ConvertImageAction().ExecuteAsync(json.RootElement, workspace.Context, CancellationToken.None)).Success);
        Assert.True(File.Exists(converted));
        var resized = workspace.PathOf("resized.png");
        using (var json = JsonDocument.Parse(JsonSerializer.Serialize(new { source, destination = resized, width = 200, height = (int?)null, keepAspectRatio = true })))
            Assert.True((await new ResizeImageAction().ExecuteAsync(json.RootElement, workspace.Context, CancellationToken.None)).Success);
        using var result = await Image.LoadAsync(resized);
        Assert.Equal(100, result.Width);
        Assert.Equal(50, result.Height);
    }

    [Fact]
    public async Task ValidatesQualityAndWorkspaceDestination()
    {
        using var workspace = new TestWorkspace();
        var source = workspace.PathOf("source.png");
        using (var image = new Image<Rgba32>(10, 10)) await image.SaveAsPngAsync(source);
        using var invalidQuality = JsonDocument.Parse(JsonSerializer.Serialize(new { source, destination = workspace.PathOf("out.webp"), format = "webp", quality = 101 }));
        Assert.Equal(ErrorCodes.InvalidRequest, (await new ConvertImageAction().ExecuteAsync(invalidQuality.RootElement, workspace.Context, CancellationToken.None)).Error?.Code);
        using var outside = JsonDocument.Parse(JsonSerializer.Serialize(new { source, destination = Path.Combine(Path.GetTempPath(), "outside.webp"), format = "webp", quality = 80 }));
        var error = await Assert.ThrowsAsync<DeskBridge.Core.Actions.DeskBridgeActionException>(() => new ConvertImageAction().ExecuteAsync(outside.RootElement, workspace.Context, CancellationToken.None));
        Assert.Equal(ErrorCodes.WorkspaceViolation, error.Code);
    }
}
