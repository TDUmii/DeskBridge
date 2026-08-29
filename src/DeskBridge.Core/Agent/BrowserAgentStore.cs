using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using DeskBridge.Core.Models;
using DeskBridge.Core.Security;

namespace DeskBridge.Core.Agent;

public sealed class BrowserAgentStore(string? root = null, ArtifactInspector? inspector = null)
{
    private const int MaximumChunkBytes = 500_000;
    private readonly string _root = root ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DeskBridge", "web-agent");
    private readonly ArtifactInspector _inspector = inspector ?? new ArtifactInspector();
    private readonly string _mutexName = root is null ? @"Local\DeskBridge.WebAgentStore.v1" : $@"Local\DeskBridge.WebAgentStore.Tests.{Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(root)))[..16]}";

    public BrowserAgentJob Create(AgentRunRequest request)
    {
        var guard = new WorkspaceGuard(request.Workspace);
        var source = guard.EnsureInside(request.SourcePath, false);
        if (!File.Exists(source)) throw new FileNotFoundException("The selected source file does not exist.", source);
        if (string.IsNullOrWhiteSpace(request.UserRequest)) throw new ArgumentException("Describe the finished result you want.", nameof(request));

        return Locked(() =>
        {
            var id = $"{DateTime.UtcNow:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}";
            var runDirectory = Path.Combine(request.Workspace, ".deskbridge", "web-agent-runs", id);
            var originalDirectory = Path.Combine(runDirectory, "original");
            Directory.CreateDirectory(originalDirectory);
            Directory.CreateDirectory(Path.Combine(runDirectory, "versions"));
            Directory.CreateDirectory(Path.Combine(runDirectory, "inspection"));
            var preserved = Path.Combine(originalDirectory, Path.GetFileName(source));
            File.Copy(source, preserved, true);
            var now = DateTimeOffset.UtcNow;
            var job = new BrowserAgentJob
            {
                Id = id, Workspace = request.Workspace, SourcePath = source, PreservedSourcePath = preserved,
                SourceFileName = Path.GetFileName(source), SourceSize = new FileInfo(source).Length,
                UserRequest = request.UserRequest.Trim(), MaximumIterations = request.Options.MaximumIterations,
                CandidateToken = $"deskbridge-{Guid.NewGuid():N}"[..23], CreatedAt = now, UpdatedAt = now,
                RunDirectory = runDirectory
            };
            Write(job);
            return job;
        });
    }

    public BrowserAgentJob? Read(string id) => Locked(() => ReadUnsafe(id));

    public BrowserAgentClaim? ClaimNext()
    {
        return Locked(() =>
        {
            Directory.CreateDirectory(RunsRoot);
            var job = Directory.EnumerateFiles(RunsRoot, "job.json", SearchOption.AllDirectories)
                .Select(ReadFile).Where(item => item.Status == "pending").OrderBy(item => item.CreatedAt).FirstOrDefault();
            if (job is null) return null;
            job = job with { Status = "claimed", Stage = "Connected", Message = "ChatGPT Web claimed this file task.", Iteration = 1, UpdatedAt = DateTimeOffset.UtcNow };
            Write(job);
            return new BrowserAgentClaim(job.Id, job.SourceFileName, job.SourceSize, BuildInitialPrompt(job), job.RequiredModel,
                job.RequiredReasoning, job.MaximumIterations, job.CandidateToken);
        });
    }

    public object ReadSourceChunk(string id, long offset, int requestedBytes)
    {
        if (offset < 0) throw new ArgumentOutOfRangeException(nameof(offset));
        var count = Math.Clamp(requestedBytes, 1, MaximumChunkBytes);
        var job = Read(id) ?? throw new InvalidOperationException("Unknown web-agent job.");
        if (job.Status is "cancelled" or "failed") throw new InvalidOperationException("This web-agent job is no longer active.");
        using var stream = File.OpenRead(job.PreservedSourcePath);
        if (offset > stream.Length) throw new ArgumentOutOfRangeException(nameof(offset));
        stream.Position = offset;
        var buffer = new byte[Math.Min(count, checked((int)Math.Min(int.MaxValue, stream.Length - offset)))];
        var read = stream.Read(buffer, 0, buffer.Length);
        return new { offset, bytes = Convert.ToBase64String(buffer, 0, read), nextOffset = offset + read, complete = offset + read >= stream.Length, total = stream.Length };
    }

    public BrowserAgentJob UpdateProgress(string id, string stage, string message, string? chatUrl = null)
    {
        return Locked(() =>
        {
            var job = ReadUnsafe(id) ?? throw new InvalidOperationException("Unknown web-agent job.");
            if (job.Status is "completed" or "needs_review" or "failed" or "cancelled") return job;
            job = job with { Stage = Bound(stage, 80), Message = Bound(message, 500), ChatUrl = chatUrl ?? job.ChatUrl, UpdatedAt = DateTimeOffset.UtcNow };
            Write(job);
            return job;
        });
    }

    public async Task<BrowserCandidateResult> InspectCandidateAsync(string id, string downloadedPath, BrowserCandidateAssessment assessment, CancellationToken cancellationToken)
    {
        var snapshot = Read(id) ?? throw new InvalidOperationException("Unknown web-agent job.");
        if (snapshot.Status is "completed" or "needs_review" or "failed" or "cancelled")
            throw new InvalidOperationException("This web-agent job is already terminal.");
        if (!File.Exists(downloadedPath)) throw new FileNotFoundException("The ChatGPT Web download was not found.", downloadedPath);
        if (new FileInfo(downloadedPath).LastWriteTimeUtc < snapshot.CreatedAt.UtcDateTime.AddMinutes(-1))
            throw new InvalidOperationException("The downloaded candidate predates this web-agent run.");
        var safeName = Path.GetFileName(assessment.CandidateFile);
        if (!string.Equals(Path.GetFileName(downloadedPath), safeName, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("The downloaded filename does not match ChatGPT's candidate envelope.");
        if (!safeName.Contains(snapshot.CandidateToken, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("The candidate filename does not contain this run's safety token.");
        if (assessment.Score is < 0 or > 100) throw new ArgumentOutOfRangeException(nameof(assessment), "Score must be between 0 and 100.");

        var versionDirectory = Path.Combine(snapshot.RunDirectory, "versions", $"v{snapshot.Iteration:00}");
        Directory.CreateDirectory(versionDirectory);
        var localPath = UniqueDestination(versionDirectory, safeName);
        File.Copy(downloadedPath, localPath, false);
        var inspection = await _inspector.InspectAsync(localPath, Path.Combine(snapshot.RunDirectory, "inspection"), cancellationToken).ConfigureAwait(false);
        var accepted = assessment.Score >= 90 && assessment.RemainingIssues.Count == 0;
        var terminal = accepted || snapshot.Iteration >= snapshot.MaximumIterations;
        string? finalPath = null;
        if (accepted)
        {
            var resultsDirectory = Path.Combine(snapshot.Workspace, "DeskBridge Results");
            Directory.CreateDirectory(resultsDirectory);
            finalPath = UniqueDestination(resultsDirectory, safeName);
            File.Copy(localPath, finalPath, false);
        }

        var followUp = terminal ? null : BuildFollowUp(snapshot, assessment, inspection, localPath);
        var updated = Locked(() =>
        {
            var current = ReadUnsafe(id) ?? throw new InvalidOperationException("Unknown web-agent job.");
            var status = accepted ? "completed" : terminal ? "needs_review" : "claimed";
            var stage = accepted ? "Completed" : terminal ? "Review needed" : "Revising";
            var message = accepted ? "The ChatGPT Web candidate passed the completion gate." : terminal
                ? "The maximum web revision count was reached; the best candidate was preserved."
                : $"Local inspection finished. Sending revision {current.Iteration + 1} to ChatGPT Web.";
            current = current with
            {
                Status = status, Stage = stage, Message = message, Iteration = terminal ? current.Iteration : current.Iteration + 1,
                BestArtifactPath = finalPath ?? localPath, Score = assessment.Score, Summary = assessment.Summary,
                RemainingIssues = assessment.RemainingIssues, UpdatedAt = DateTimeOffset.UtcNow
            };
            Write(current);
            return current;
        });

        return new BrowserCandidateResult(accepted, terminal, updated.Status, updated.BestArtifactPath!, updated.Summary,
            assessment.Score, assessment.RemainingIssues, followUp);
    }

    public BrowserAgentJob Fail(string id, string message) => Locked(() =>
    {
        var job = ReadUnsafe(id) ?? throw new InvalidOperationException("Unknown web-agent job.");
        if (job.Status is "completed" or "needs_review" or "failed" or "cancelled") return job;
        job = job with { Status = "failed", Stage = "Web failed", Message = Bound(message, 1_000), Summary = Bound(message, 1_000), UpdatedAt = DateTimeOffset.UtcNow };
        Write(job);
        return job;
    });

    public BrowserAgentJob Cancel(string id) => Locked(() =>
    {
        var job = ReadUnsafe(id) ?? throw new InvalidOperationException("Unknown web-agent job.");
        if (job.Status is "completed" or "needs_review" or "failed") return job;
        job = job with { Status = "cancelled", Stage = "Cancelled", Message = "The local request was cancelled. ChatGPT Web will not receive another prompt.", UpdatedAt = DateTimeOffset.UtcNow };
        Write(job);
        return job;
    });

    private string RunsRoot => Path.Combine(_root, "runs");
    private string JobFile(string id) => Path.Combine(RunsRoot, id, "job.json");
    private BrowserAgentJob? ReadUnsafe(string id) => File.Exists(JobFile(id)) ? ReadFile(JobFile(id)) : null;
    private static BrowserAgentJob ReadFile(string path) => JsonSerializer.Deserialize<BrowserAgentJob>(File.ReadAllText(path), DeskBridgeJson.Options)
        ?? throw new InvalidDataException($"Invalid web-agent job at {path}.");
    private void Write(BrowserAgentJob job)
    {
        var path = JobFile(job.Id);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temporary = path + $".{Environment.ProcessId}.tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(job, DeskBridgeJson.Options), new UTF8Encoding(false));
        File.Move(temporary, path, true);
    }

    private T Locked<T>(Func<T> action)
    {
        using var mutex = new Mutex(false, _mutexName);
        var acquired = false;
        try
        {
            try { acquired = mutex.WaitOne(TimeSpan.FromSeconds(10)); }
            catch (AbandonedMutexException) { acquired = true; }
            if (!acquired) throw new TimeoutException("Timed out waiting for the local web-agent store.");
            return action();
        }
        finally { if (acquired) mutex.ReleaseMutex(); }
    }

    private static string BuildInitialPrompt(BrowserAgentJob job) => $$"""
        You are completing a DeskBridge Web-only file task. Work only in this ChatGPT Web conversation.
        Do not suggest Codex, Codex tasks, Codex workspace, API keys, the OpenAI API, or any external agent.

        User request:
        {{job.UserRequest}}

        The source file is attached. Produce a finished downloadable artifact, not instructions.
        Preserve correct content and improve only what the request requires. Inspect your own output carefully.
        The output filename MUST contain this exact safety token: {{job.CandidateToken}}

        At the end of your response, include exactly one JSON code block with this schema:
        {"deskbridgeAgent":1,"candidateFile":"filename-containing-the-safety-token.ext","score":95,"summary":"what changed","requirementsMet":["specific evidence"],"remainingIssues":[]}

        Attach a direct downloadable file whose filename exactly matches candidateFile. Do not claim completion without attaching it.
        Score honestly. Use score >= 90 and an empty remainingIssues only when every material requirement is satisfied.
        """;

    private static string BuildFollowUp(BrowserAgentJob job, BrowserCandidateAssessment assessment, LocalArtifactInspection inspection, string localPath) => $$"""
        DeskBridge downloaded and inspected candidate {{job.Iteration}} locally.
        Local file: {{Path.GetFileName(localPath)}}
        Evidence: {{inspection.Summary}}
        Your prior score: {{assessment.Score}}
        Remaining issues: {{(assessment.RemainingIssues.Count == 0 ? "none declared" : string.Join("; ", assessment.RemainingIssues))}}

        Re-check the original user request and create a materially improved replacement. Do not merely describe changes.
        The new filename MUST contain the same safety token: {{job.CandidateToken}}
        Attach the new file and finish with the same single deskbridgeAgent JSON block. This is revision {{job.Iteration + 1}} of {{job.MaximumIterations}}.
        """;

    private static string UniqueDestination(string directory, string filename)
    {
        var path = Path.Combine(directory, filename);
        if (!File.Exists(path)) return path;
        var stem = Path.GetFileNameWithoutExtension(filename);
        var extension = Path.GetExtension(filename);
        for (var index = 2; index < 10_000; index++)
        {
            path = Path.Combine(directory, $"{stem}-{index}{extension}");
            if (!File.Exists(path)) return path;
        }
        throw new IOException("Could not allocate a unique candidate filename.");
    }

    private static string Bound(string value, int maximum)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        var trimmed = value.Trim();
        return trimmed[..Math.Min(maximum, trimmed.Length)];
    }
}
