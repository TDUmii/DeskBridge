namespace DeskBridge.Core.Skills;

public static class SkillCatalog
{
    public const string ConvertDocumentsToMarkdown = "convert_documents_to_markdown";
    public const string Impeccable = "impeccable";

    public static readonly IReadOnlyList<SkillDefinition> All =
    [
        new(ConvertDocumentsToMarkdown, "Convert documents to Markdown", "Executable adapter",
            "Converts supported workspace documents with @firecrawl/anydoc. Node.js 20+ and npm are required; the first run may download the converter from npm.",
            "Use convert_document_to_markdown only after the user chooses a source and .md destination inside the active workspace."),
        new(Impeccable, "Impeccable", "Guidance profile",
            "Adds a reusable UI-quality instruction for ChatGPT. DeskBridge does not run a local design model.",
            "For UI work, apply an Impeccable-style workflow: preserve product truth, establish clear hierarchy, support light and dark themes, cover keyboard, focus, empty, loading and error states, and verify the rendered interface."),
    ];

    public static bool IsEnabled(DeskBridge.Core.Models.DeskBridgeSettings settings, string id) =>
        settings.SkillIntegrations.GetValueOrDefault(id, false);
}

public sealed record SkillDefinition(
    string Id,
    string Name,
    string Kind,
    string Description,
    string Instruction);
