using System.Text;
using System.Text.Json;
using DeskBridge.Core.Models;
using DeskBridge.Core.Security;

namespace DeskBridge.Core.Agent;

public sealed class AgentRunService(IOpenAIResponsesClient client, ArtifactInspector? inspector = null)
{
    private readonly ArtifactInspector _inspector = inspector ?? new ArtifactInspector();

    public async Task<AgentRunResult> RunAsync(AgentRunRequest request, IProgress<AgentProgress>? progress, CancellationToken cancellationToken)
    {
        ValidateOptions(request.Options);
        var guard = new WorkspaceGuard(request.Workspace);
        var source = guard.EnsureInside(request.SourcePath, false);
        if (!File.Exists(source)) throw new FileNotFoundException("The selected source file does not exist.", source);
        if (string.IsNullOrWhiteSpace(request.UserRequest)) throw new ArgumentException("Describe the result you want.", nameof(request));

        var runId = $"{DateTime.UtcNow:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}"[..32];
        var runDirectory = guard.EnsureInside(Path.Combine(request.Workspace, ".deskbridge", "agent-runs", runId), false);
        var originalDirectory = Path.Combine(runDirectory, "original");
        var versionsDirectory = Path.Combine(runDirectory, "versions");
        var inspectionDirectory = Path.Combine(runDirectory, "inspection");
        Directory.CreateDirectory(originalDirectory);
        Directory.CreateDirectory(versionsDirectory);
        Directory.CreateDirectory(inspectionDirectory);
        var original = Path.Combine(originalDirectory, Path.GetFileName(source));
        File.Copy(source, original, true);

        await WriteJsonAsync(Path.Combine(runDirectory, "task.json"), new
        {
            id = runId, createdAt = DateTimeOffset.UtcNow, request.SourcePath, request.UserRequest, request.Options,
            privacy = "The selected file and prompt are sent to the OpenAI API. Local inspections stay on this device unless included in tool results."
        }, cancellationToken).ConfigureAwait(false);

        Report(progress, "Inspecting", "Inspecting the original artifact locally.", 0, 0, source);
        var originalInspection = await _inspector.InspectAsync(original, inspectionDirectory, cancellationToken).ConfigureAwait(false);
        Report(progress, "Uploading", "Uploading the selected artifact to OpenAI.", 0, 0, Path.GetFileName(source));
        var inputFileId = await client.UploadFileAsync(original, cancellationToken).ConfigureAwait(false);

        var dispatcher = new AgentToolDispatcher(guard, runDirectory, versionsDirectory, inspectionDirectory, client, _inspector);
        var instructions = BuildInstructions(request, runDirectory);
        var prompt = BuildPrompt(request, originalInspection);
        AgentUsage usage = new(0, 0, 0);
        AgentResponse response = await client.CreateInitialAsync(instructions, prompt, inputFileId, request.Options, cancellationToken).ConfigureAwait(false);
        var toolCalls = 0;

        var iteration = 1;
        while (iteration <= request.Options.MaximumIterations)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Report(progress, "Reasoning", $"Agent iteration {iteration} of {request.Options.MaximumIterations}.", iteration, toolCalls);
            usage += response.Usage;
            if (!string.IsNullOrWhiteSpace(response.ErrorMessage)) throw new InvalidOperationException(response.ErrorMessage);

            if (response.ToolCalls.Count == 0)
            {
                if (dispatcher.Completion is not null) break;
                throw new InvalidOperationException(string.IsNullOrWhiteSpace(response.OutputText)
                    ? "The model stopped without producing a tool call or final artifact."
                    : $"The model stopped before finalizing an artifact: {response.OutputText}");
            }

            var outputs = new List<ToolOutput>();
            foreach (var call in response.ToolCalls)
            {
                toolCalls++;
                if (toolCalls > request.Options.MaximumToolCalls)
                    throw new InvalidOperationException($"Agent exceeded the {request.Options.MaximumToolCalls}-tool-call safety limit.");
                Report(progress, "Tool", DescribeTool(call.Name), iteration, toolCalls, call.Name);
                outputs.Add(await dispatcher.ExecuteAsync(call, cancellationToken).ConfigureAwait(false));
                if (dispatcher.Completion is not null) break;
            }

            if (dispatcher.Completion is not null) break;
            if (iteration == request.Options.MaximumIterations) break;
            response = await client.ContinueAsync(instructions, response.Id, outputs, request.Options, cancellationToken).ConfigureAwait(false);
            iteration++;
        }

        var completion = dispatcher.Completion;
        AgentRunResult result;
        if (completion is not null)
        {
            Report(progress, "Completed", "The best artifact passed the agent completion gate.", request.Options.MaximumIterations, toolCalls, completion.BestArtifactPath);
            result = new AgentRunResult(true, "completed", completion.BestArtifactPath, completion.Score,
                completion.Summary, completion.RemainingIssues, usage, runDirectory);
        }
        else if (dispatcher.BestCandidate is not null)
        {
            var best = dispatcher.BestCandidate;
            result = new AgentRunResult(false, "needs_review", best.Path, best.Score,
                "The safety limit was reached. The best inspected candidate was preserved for review.", ["The model did not complete the acceptance gate."], usage, runDirectory);
        }
        else
        {
            result = new AgentRunResult(false, "failed", null, null,
                "No candidate artifact was produced before the safety limit.", ["No generated artifact was available."], usage, runDirectory);
        }

        await WriteJsonAsync(Path.Combine(runDirectory, "result.json"), result, cancellationToken).ConfigureAwait(false);
        return result;
    }

    private static string BuildInstructions(AgentRunRequest request, string runDirectory) => $$"""
        You are the DeskBridge Artifact Agent. Complete the user's file task through evidence-backed iterations.
        The selected source is attached. Preserve the user's intent and all correct existing content.
        Use Code Interpreter to create or revise binary artifacts such as DOCX, PPTX, XLSX, PDF, and images.
        Use local function tools for workspace inspection and text artifacts. Never invent inspection results.
        For every generated candidate, call save_generated_file with its actual file_id, then study the returned local inspection.
        Before completion, compare the candidate against every explicit request and the original artifact.
        Call complete_task only with an existing inspected local candidate. Aim for score >= 90 and no material remaining issue.
        If a candidate regresses, create a better version instead of finalizing it.
        Do not delete the user's original file. Do not access paths outside the workspace.
        Maximum iterations: {{request.Options.MaximumIterations}}. Maximum tool calls: {{request.Options.MaximumToolCalls}}.
        Local run directory: {{runDirectory}}
        """;

    private static string BuildPrompt(AgentRunRequest request, LocalArtifactInspection inspection) => $$"""
        User request:
        {{request.UserRequest.Trim()}}

        Local preserved source copy: {{inspection.Path}}
        Local inspection: {{inspection.Summary}}

        Deliver a finished file, not instructions or a placeholder. Inspect, improve, validate, and finalize the best candidate.
        """;

    private static void ValidateOptions(AgentRunOptions options)
    {
        if (options.Model is not ("gpt-5.6-luna" or "gpt-5.6-terra" or "gpt-5.6-sol")) throw new ArgumentException("Unsupported agent model.");
        if (options.ReasoningEffort is not ("none" or "low" or "medium" or "high" or "xhigh" or "max")) throw new ArgumentException("Unsupported reasoning effort.");
        if (options.MaximumIterations is < 1 or > 8) throw new ArgumentOutOfRangeException(nameof(options), "Maximum iterations must be between 1 and 8.");
        if (options.MaximumToolCalls is < 1 or > 64) throw new ArgumentOutOfRangeException(nameof(options), "Maximum tool calls must be between 1 and 64.");
        if (options.MaximumOutputTokensPerTurn is < 1_000 or > 32_000) throw new ArgumentOutOfRangeException(nameof(options), "Maximum output tokens must be between 1000 and 32000.");
    }

    private static string DescribeTool(string name) => name switch
    {
        "inspect_local_file" => "Inspecting a local file.", "read_text_file" => "Reading bounded text.",
        "write_text_file" => "Writing a text candidate.", "list_folder" => "Inspecting a workspace folder.",
        "save_generated_file" => "Downloading and inspecting a generated candidate.",
        "complete_task" => "Validating the completion gate.", _ => $"Running {name}."
    };
    private static void Report(IProgress<AgentProgress>? progress, string stage, string message, int iteration, int toolCalls, string? detail = null) =>
        progress?.Report(new AgentProgress(stage, message, iteration, toolCalls, detail));
    private static async Task WriteJsonAsync(string path, object value, CancellationToken cancellationToken)
    {
        await using var stream = File.Create(path);
        await JsonSerializer.SerializeAsync(stream, value, DeskBridgeJson.Options, cancellationToken).ConfigureAwait(false);
    }
}

internal sealed class AgentToolDispatcher(
    WorkspaceGuard guard,
    string runDirectory,
    string versionsDirectory,
    string inspectionDirectory,
    IOpenAIResponsesClient client,
    ArtifactInspector inspector)
{
    private readonly List<AgentCandidate> _candidates = [];
    public AgentCompletion? Completion { get; private set; }
    public AgentCandidate? BestCandidate => _candidates.OrderByDescending(candidate => candidate.Score).FirstOrDefault();

    public async Task<ToolOutput> ExecuteAsync(ResponseToolCall call, CancellationToken cancellationToken)
    {
        try
        {
            var result = call.Name switch
            {
                "inspect_local_file" => await InspectAsync(call.Arguments, cancellationToken).ConfigureAwait(false),
                "read_text_file" => await ReadAsync(call.Arguments, cancellationToken).ConfigureAwait(false),
                "write_text_file" => await WriteAsync(call.Arguments, cancellationToken).ConfigureAwait(false),
                "list_folder" => List(call.Arguments),
                "save_generated_file" => await SaveGeneratedAsync(call.Arguments, cancellationToken).ConfigureAwait(false),
                "complete_task" => await CompleteAsync(call.Arguments, cancellationToken).ConfigureAwait(false),
                _ => new { success = false, error = $"Unknown agent tool '{call.Name}'." }
            };
            return new ToolOutput(call.CallId, JsonSerializer.Serialize(result, DeskBridgeJson.Options));
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return new ToolOutput(call.CallId, JsonSerializer.Serialize(new { success = false, error = exception.Message }, DeskBridgeJson.Options));
        }
    }

    private async Task<object> InspectAsync(JsonElement arguments, CancellationToken cancellationToken)
    {
        var path = EnsureRunPath(RequiredString(arguments, "path"));
        var inspection = await inspector.InspectAsync(path, inspectionDirectory, cancellationToken).ConfigureAwait(false);
        return new { success = true, inspection };
    }

    private async Task<object> ReadAsync(JsonElement arguments, CancellationToken cancellationToken)
    {
        var path = EnsureRunPath(RequiredString(arguments, "path"));
        var maximum = arguments.GetProperty("maxCharacters").GetInt32();
        if (maximum is < 100 or > 50_000) throw new ArgumentOutOfRangeException(nameof(arguments), "maxCharacters must be between 100 and 50000.");
        using var reader = new StreamReader(path, Encoding.UTF8, true);
        var buffer = new char[maximum];
        var read = await reader.ReadBlockAsync(buffer.AsMemory(), cancellationToken).ConfigureAwait(false);
        return new { success = true, path, content = new string(buffer, 0, read), truncated = !reader.EndOfStream };
    }

    private async Task<object> WriteAsync(JsonElement arguments, CancellationToken cancellationToken)
    {
        var path = EnsureRunPath(RequiredString(arguments, "path"));
        var content = RequiredString(arguments, "content", allowEmpty: true);
        var overwrite = arguments.GetProperty("overwrite").GetBoolean();
        if (File.Exists(path) && !overwrite) return new { success = false, error = "File exists and overwrite is false." };
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(path, content, new UTF8Encoding(false), cancellationToken).ConfigureAwait(false);
        var inspection = await inspector.InspectAsync(path, inspectionDirectory, cancellationToken).ConfigureAwait(false);
        _candidates.Add(new AgentCandidate(path, 70, inspection));
        return new { success = true, inspection, note = "Text candidate saved. Inspect requirements before completion." };
    }

    private object List(JsonElement arguments)
    {
        var path = EnsureRunPath(RequiredString(arguments, "path"));
        var entries = Directory.EnumerateFileSystemEntries(path).Take(200).Select(item => new
        { name = Path.GetFileName(item), type = Directory.Exists(item) ? "folder" : "file" }).ToArray();
        return new { success = true, path, entries };
    }

    private async Task<object> SaveGeneratedAsync(JsonElement arguments, CancellationToken cancellationToken)
    {
        var fileId = RequiredString(arguments, "file_id");
        var filename = Path.GetFileName(RequiredString(arguments, "filename"));
        if (string.IsNullOrWhiteSpace(filename) || filename.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0) throw new ArgumentException("Generated filename is invalid.");
        var score = arguments.GetProperty("claimed_score").GetInt32();
        if (score is < 0 or > 100) throw new ArgumentOutOfRangeException(nameof(arguments), "Score must be between 0 and 100.");
        var versionDirectory = Path.Combine(versionsDirectory, $"v{_candidates.Count + 1:00}");
        var destination = Path.Combine(versionDirectory, filename);
        await client.DownloadFileAsync(fileId, destination, cancellationToken).ConfigureAwait(false);
        var inspection = await inspector.InspectAsync(destination, inspectionDirectory, cancellationToken).ConfigureAwait(false);
        var candidate = new AgentCandidate(destination, score, inspection);
        _candidates.Add(candidate);
        return new { success = true, candidate.Path, candidate.Score, inspection, summary = RequiredString(arguments, "summary"), instruction = "Compare this evidence against every user requirement. Revise if any material issue remains." };
    }

    private async Task<object> CompleteAsync(JsonElement arguments, CancellationToken cancellationToken)
    {
        var bestPath = Path.GetFullPath(RequiredString(arguments, "best_path"));
        var candidate = _candidates.FirstOrDefault(item => string.Equals(Path.GetFullPath(item.Path), bestPath, StringComparison.OrdinalIgnoreCase));
        if (candidate is null) return new { success = false, error = "best_path is not an inspected candidate. Inspect or save it first." };
        var score = arguments.GetProperty("score").GetInt32();
        var issues = ReadStringArray(arguments, "remaining_issues");
        if (score < 90 || issues.Count > 0)
            return new { success = false, error = "Completion gate requires score >= 90 and no remaining issues. Produce a better candidate.", currentScore = score, remainingIssues = issues };

        var resultDirectory = guard.EnsureInside(Path.Combine(guard.WorkspaceRoot, "DeskBridge Results"), false);
        Directory.CreateDirectory(resultDirectory);
        var destination = UniqueDestination(resultDirectory, Path.GetFileName(bestPath));
        File.Copy(bestPath, destination, false);
        var finalInspection = await inspector.InspectAsync(destination, inspectionDirectory, cancellationToken).ConfigureAwait(false);
        Completion = new AgentCompletion(destination, score, RequiredString(arguments, "summary"), ReadStringArray(arguments, "requirements_met"), issues, finalInspection);
        return new { success = true, finalPath = destination, score, finalInspection };
    }

    private static string UniqueDestination(string directory, string filename)
    {
        var path = Path.Combine(directory, filename);
        if (!File.Exists(path)) return path;
        var name = Path.GetFileNameWithoutExtension(filename);
        var extension = Path.GetExtension(filename);
        for (var index = 2; index < 10_000; index++)
        {
            path = Path.Combine(directory, $"{name}-{index}{extension}");
            if (!File.Exists(path)) return path;
        }
        throw new IOException("Could not allocate a unique result filename.");
    }

    private static string RequiredString(JsonElement arguments, string name, bool allowEmpty = false)
    {
        if (!arguments.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.String || (!allowEmpty && string.IsNullOrWhiteSpace(value.GetString())))
            throw new ArgumentException($"'{name}' must be a {(allowEmpty ? string.Empty : "non-empty ")}string.");
        return value.GetString() ?? string.Empty;
    }
    private static IReadOnlyList<string> ReadStringArray(JsonElement arguments, string name) =>
        arguments.GetProperty(name).EnumerateArray().Select(item => item.GetString() ?? string.Empty).Where(item => !string.IsNullOrWhiteSpace(item)).ToArray();

    private string EnsureRunPath(string path)
    {
        var normalized = guard.EnsureInside(path, false);
        var root = Path.GetFullPath(runDirectory).TrimEnd(Path.DirectorySeparatorChar);
        if (!string.Equals(normalized, root, StringComparison.OrdinalIgnoreCase) &&
            !normalized.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new UnauthorizedAccessException("Agent tools may access only the current run directory and its preserved source copy.");
        return normalized;
    }
}

internal sealed record AgentCandidate(string Path, int Score, LocalArtifactInspection Inspection);
internal sealed record AgentCompletion(string BestArtifactPath, int Score, string Summary, IReadOnlyList<string> RequirementsMet, IReadOnlyList<string> RemainingIssues, LocalArtifactInspection Inspection);
