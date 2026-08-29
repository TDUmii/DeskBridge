using System.Diagnostics;
using Microsoft.Win32;

namespace DeskBridge.Core.Agent;

public sealed class AgentRunService(BrowserAgentStore store)
{
    public async Task<AgentRunResult> RunAsync(AgentRunRequest request, IProgress<AgentProgress>? progress, CancellationToken cancellationToken)
    {
        if (request.Options.MaximumIterations is < 1 or > 8)
            throw new ArgumentOutOfRangeException(nameof(request), "Maximum iterations must be between 1 and 8.");
        if (request.Options.PollIntervalMilliseconds is < 250 or > 5_000)
            throw new ArgumentOutOfRangeException(nameof(request), "Poll interval must be between 250 and 5000 milliseconds.");

        var job = store.Create(request);
        progress?.Report(new AgentProgress(job.Stage, job.Message, job.Iteration, job.RunDirectory));
        try
        {
            ChromeLauncher.OpenChatGpt();
        }
        catch (Exception exception)
        {
            store.Fail(job.Id, exception.Message);
            throw;
        }
        var lastUpdate = job.UpdatedAt;

        try
        {
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await Task.Delay(request.Options.PollIntervalMilliseconds, cancellationToken).ConfigureAwait(false);
                job = store.Read(job.Id) ?? throw new InvalidOperationException("The local web-agent job disappeared.");
                if (job.UpdatedAt != lastUpdate)
                {
                    lastUpdate = job.UpdatedAt;
                    progress?.Report(new AgentProgress(job.Stage, job.Message, job.Iteration, job.ChatUrl));
                }

                if (job.Status is "completed" or "needs_review" or "failed" or "cancelled")
                    return ToResult(job);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            job = store.Cancel(job.Id);
            return ToResult(job);
        }
    }

    private static AgentRunResult ToResult(BrowserAgentJob job) => new(
        job.Status == "completed", job.Status, job.BestArtifactPath, job.Score,
        string.IsNullOrWhiteSpace(job.Summary) ? job.Message : job.Summary,
        job.RemainingIssues, job.RunDirectory, job.ChatUrl);
}

internal static class ChromeLauncher
{
    public static void OpenChatGpt()
    {
        var chrome = FindChrome() ?? throw new InvalidOperationException(
            "Google Chrome is required for ChatGPT Web-only runs. DeskBridge will not fall back to Codex, a workspace, another browser, or an API.");
        Process.Start(new ProcessStartInfo(chrome)
        {
            UseShellExecute = true,
            ArgumentList = { "--new-tab", "https://chatgpt.com/?deskbridge-agent=1" }
        });
    }

    private static string? FindChrome()
    {
        foreach (var root in new[] { Registry.CurrentUser, Registry.LocalMachine })
        {
            using var key = root.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\chrome.exe");
            if (key?.GetValue(null) is string path && File.Exists(path)) return path;
        }
        var candidates = new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Google", "Chrome", "Application", "chrome.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Google", "Chrome", "Application", "chrome.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Google", "Chrome", "Application", "chrome.exe")
        };
        return candidates.FirstOrDefault(File.Exists);
    }
}
