using System.Text.Json;
using DeskBridge.Core.Models;
using DeskBridge.Core.Skills;

namespace DeskBridge.Tests;

public sealed class SkillIntegrationTests
{
    [Fact]
    public async Task SettingsRoundTripThemeAndSkillChoices()
    {
        using var workspace = new TestWorkspace();
        var settings = new DeskBridgeSettings
        {
            ThemeMode = "dark",
            SkillIntegrations = new Dictionary<string, bool> { [SkillCatalog.Impeccable] = true }
        };
        await workspace.Context.Settings.SaveAsync(settings);

        var loaded = await workspace.Context.Settings.LoadAsync();

        Assert.Equal("dark", loaded.ThemeMode);
        Assert.True(loaded.SkillIntegrations[SkillCatalog.Impeccable]);
    }

    [Fact]
    public async Task ConversionRequiresEnabledIntegration()
    {
        using var workspace = new TestWorkspace();
        var source = workspace.PathOf("lesson.docx");
        await File.WriteAllTextAsync(source, "document");

        var result = await Execute(workspace, new FakeConverter(), source, workspace.PathOf("lesson.md"));

        Assert.Equal(ErrorCodes.SkillDisabled, result.Error?.Code);
    }

    [Fact]
    public async Task ConversionWritesMarkdownInsideWorkspace()
    {
        using var workspace = new TestWorkspace();
        await EnableConverter(workspace);
        var source = workspace.PathOf("lesson.docx");
        var destination = workspace.PathOf("notes", "lesson.md");
        await File.WriteAllTextAsync(source, "document");

        var result = await Execute(workspace, new FakeConverter("# Lesson"), source, destination);

        Assert.True(result.Success);
        Assert.Equal("# Lesson", await File.ReadAllTextAsync(destination));
    }

    [Fact]
    public async Task ConversionRejectsDestinationOutsideWorkspace()
    {
        using var workspace = new TestWorkspace();
        await EnableConverter(workspace);
        var source = workspace.PathOf("lesson.docx");
        await File.WriteAllTextAsync(source, "document");

        var error = await Assert.ThrowsAsync<DeskBridge.Core.Actions.DeskBridgeActionException>(() =>
            Execute(workspace, new FakeConverter(), source, Path.Combine(Path.GetTempPath(), "outside.md")));

        Assert.Equal(ErrorCodes.WorkspaceViolation, error.Code);
    }

    [Fact]
    public async Task ConversionDoesNotUseHostedOcr()
    {
        using var workspace = new TestWorkspace();
        await EnableConverter(workspace);
        var source = workspace.PathOf("scan.pdf");
        await File.WriteAllTextAsync(source, "scan");

        var result = await Execute(workspace, new FakeConverter(exitCode: 3), source, workspace.PathOf("scan.md"));

        Assert.Equal(ErrorCodes.DocumentOcrRequired, result.Error?.Code);
    }

    private static async Task EnableConverter(TestWorkspace workspace) =>
        await workspace.Context.Settings.SaveAsync(new DeskBridgeSettings
        {
            SkillIntegrations = new Dictionary<string, bool> { [SkillCatalog.ConvertDocumentsToMarkdown] = true }
        });

    private static async Task<ActionResult> Execute(TestWorkspace workspace, IDocumentConverter converter, string source, string destination)
    {
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(new { source, destination }));
        return await new ConvertDocumentToMarkdownAction(converter)
            .ExecuteAsync(json.RootElement, workspace.Context, CancellationToken.None);
    }

    private sealed class FakeConverter(string markdown = "# Converted", int exitCode = 0) : IDocumentConverter
    {
        public async Task<DocumentConversionResult> ConvertAsync(string source, string destination, CancellationToken cancellationToken)
        {
            if (exitCode == 0) await File.WriteAllTextAsync(destination, markdown, cancellationToken);
            return new DocumentConversionResult(exitCode, string.Empty, string.Empty);
        }
    }
}
