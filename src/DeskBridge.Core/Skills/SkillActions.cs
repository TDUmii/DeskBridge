using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json;
using DeskBridge.Core.Actions;
using DeskBridge.Core.Models;

namespace DeskBridge.Core.Skills;

public sealed class GetSkillProfileAction : IDeskBridgeAction
{
    public string Name => "get_skill_profile";

    public async Task<ActionResult> ExecuteAsync(JsonElement arguments, ActionContext context, CancellationToken cancellationToken)
    {
        var settings = await context.Settings.LoadAsync(cancellationToken).ConfigureAwait(false);
        var skills = SkillCatalog.All.Select(skill => new
        {
            skill.Id,
            skill.Name,
            skill.Kind,
            skill.Description,
            enabled = SkillCatalog.IsEnabled(settings, skill.Id),
            instruction = SkillCatalog.IsEnabled(settings, skill.Id) ? skill.Instruction : null
        }).ToArray();
        return ActionResult.Ok(new { skills });
    }
}

public interface IDocumentConverter
{
    Task<DocumentConversionResult> ConvertAsync(string source, string destination, CancellationToken cancellationToken);
}

public sealed record DocumentConversionResult(int ExitCode, string StandardOutput, string StandardError);

public sealed class AnyDocDocumentConverter : IDocumentConverter
{
    public async Task<DocumentConversionResult> ConvertAsync(string source, string destination, CancellationToken cancellationToken)
    {
        var runtime = ResolveRuntime();
        var startInfo = new ProcessStartInfo
        {
            FileName = runtime.Executable,
            WorkingDirectory = Path.GetDirectoryName(source)!,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        if (runtime.NodeDirectory is not null)
            startInfo.Environment["PATH"] = runtime.NodeDirectory + Path.PathSeparator + startInfo.Environment["PATH"];
        foreach (var argument in runtime.Arguments.Concat(["@firecrawl/anydoc", source, "-o", destination]))
            startInfo.ArgumentList.Add(argument);

        try
        {
            using var process = new Process { StartInfo = startInfo };
            if (!process.Start())
                throw new DeskBridgeActionException(ErrorCodes.SkillRuntimeUnavailable, "The document converter could not start.");

            var stdout = process.StandardOutput.ReadToEndAsync(cancellationToken);
            var stderr = process.StandardError.ReadToEndAsync(cancellationToken);
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromMinutes(5));
            try
            {
                await process.WaitForExitAsync(timeout.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                process.Kill(true);
                await process.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false);
                throw new DeskBridgeActionException(ErrorCodes.DocumentConversionFailed, "Document conversion exceeded five minutes.");
            }

            return new DocumentConversionResult(process.ExitCode, await stdout.ConfigureAwait(false), await stderr.ConfigureAwait(false));
        }
        catch (Win32Exception exception)
        {
            throw new DeskBridgeActionException(ErrorCodes.SkillRuntimeUnavailable,
                $"Node.js 20+ with npm/npx is required. {exception.Message}");
        }
    }

    private static ConverterRuntime ResolveRuntime()
    {
        var overridePath = Environment.GetEnvironmentVariable("DESKBRIDGE_NPX_PATH");
        if (!string.IsNullOrWhiteSpace(overridePath)) return new(overridePath, ["-y"], null);

        var runtimeRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".cache", "codex-runtimes", "codex-primary-runtime", "dependencies");
        var pnpm = Path.Combine(runtimeRoot, "bin", "fallback", "pnpm.cmd");
        var nodeDirectory = Path.Combine(runtimeRoot, "node", "bin");
        if (File.Exists(pnpm) && File.Exists(Path.Combine(nodeDirectory, "node.exe")))
            return new(pnpm, ["dlx"], nodeDirectory);

        return new("npx.cmd", ["-y"], null);
    }

    private sealed record ConverterRuntime(string Executable, IReadOnlyList<string> Arguments, string? NodeDirectory);
}

public sealed class ConvertDocumentToMarkdownAction(IDocumentConverter converter) : IDeskBridgeAction
{
    private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".doc", ".docx", ".docm", ".odt", ".rtf", ".epub", ".pdf", ".ppt", ".pps", ".pot",
        ".pptx", ".pptm", ".ppsx", ".ppsm", ".odp", ".xls", ".xlsx", ".xlsm", ".xlsb", ".ods", ".csv"
    };

    public string Name => "convert_document_to_markdown";

    public async Task<ActionResult> ExecuteAsync(JsonElement arguments, ActionContext context, CancellationToken cancellationToken)
    {
        var settings = await context.Settings.LoadAsync(cancellationToken).ConfigureAwait(false);
        if (!SkillCatalog.IsEnabled(settings, SkillCatalog.ConvertDocumentsToMarkdown))
            return ActionResult.Fail(ErrorCodes.SkillDisabled, "Enable Convert documents to Markdown in DeskBridge Settings first.");

        var source = context.WorkspaceGuard.EnsureInside(arguments.RequiredString("source"), false);
        var destination = context.WorkspaceGuard.EnsureInside(arguments.RequiredString("destination"), false);
        var overwrite = arguments.OptionalBool("overwrite", false);
        if (!File.Exists(source)) return ActionResult.Fail(ErrorCodes.FileNotFound, "Source document does not exist.");
        if (!SupportedExtensions.Contains(Path.GetExtension(source)))
            return ActionResult.Fail(ErrorCodes.UnsupportedDocumentFormat, "The source document format is not supported.");
        if (!string.Equals(Path.GetExtension(destination), ".md", StringComparison.OrdinalIgnoreCase))
            return ActionResult.Fail(ErrorCodes.InvalidRequest, "Destination must use the .md extension.");
        if (string.Equals(source, destination, StringComparison.OrdinalIgnoreCase))
            return ActionResult.Fail(ErrorCodes.InvalidRequest, "Source and destination must be different files.");
        if (File.Exists(destination) && !overwrite)
            return ActionResult.Fail(ErrorCodes.FileAlreadyExists, "Destination exists and overwrite is false.");

        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        var temporary = destination + ".deskbridge-tmp.md";
        try
        {
            var result = await converter.ConvertAsync(source, temporary, cancellationToken).ConfigureAwait(false);
            if (result.ExitCode == 3)
                return ActionResult.Fail(ErrorCodes.DocumentOcrRequired, "The document requires OCR. DeskBridge does not upload documents for hosted OCR.");
            if (result.ExitCode != 0 || !File.Exists(temporary))
                return ActionResult.Fail(ErrorCodes.DocumentConversionFailed,
                    string.IsNullOrWhiteSpace(result.StandardError) ? "Document conversion failed." : Truncate(result.StandardError.Trim(), 2_000));

            File.Move(temporary, destination, overwrite);
            return ActionResult.Ok(new { source, destination, size = new FileInfo(destination).Length, converter = "@firecrawl/anydoc", hostedOcr = false });
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }

    private static string Truncate(string value, int maximumLength) =>
        value.Length <= maximumLength ? value : value[..maximumLength] + "…";
}
