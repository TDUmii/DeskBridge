using System.Diagnostics;
using System.Text.Json;
using DeskBridge.Core.Models;

namespace DeskBridge.Core.Actions;

public sealed class RunCommandAction(CommandRunner runner) : IDeskBridgeAction
{
    public string Name => "run_command";

    public async Task<ActionResult> ExecuteAsync(JsonElement arguments, ActionContext context, CancellationToken cancellationToken)
    {
        var program = arguments.RequiredString("program");
        var workingDirectory = context.WorkspaceGuard.EnsureInside(arguments.RequiredString("workingDirectory"));
        if (!Directory.Exists(workingDirectory))
        {
            return ActionResult.Fail(ErrorCodes.ExecutionFailed, "Working directory does not exist.");
        }

        if (!arguments.TryGetProperty("args", out var argsElement) || argsElement.ValueKind != JsonValueKind.Array ||
            argsElement.EnumerateArray().Any(value => value.ValueKind != JsonValueKind.String))
        {
            return ActionResult.Fail(ErrorCodes.InvalidRequest, "'args' must be an array of strings.");
        }

        var args = argsElement.EnumerateArray().Select(value => value.GetString() ?? string.Empty).ToArray();
        var timeoutMs = arguments.OptionalInt("timeoutMs") ?? 30_000;
        if (timeoutMs is < 100 or > 300_000)
        {
            return ActionResult.Fail(ErrorCodes.InvalidRequest, "timeoutMs must be between 100 and 300000.");
        }

        return await runner.RunAsync(program, args, workingDirectory, TimeSpan.FromMilliseconds(timeoutMs), cancellationToken)
            .ConfigureAwait(false);
    }
}

public sealed class CommandRunner
{
    private static readonly HashSet<string> AllowedPrograms = new(StringComparer.OrdinalIgnoreCase)
        { "git", "git.exe", "python", "python.exe", "py", "py.exe", "dotnet", "dotnet.exe", "node", "node.exe", "npm", "npm.cmd" };

    private static readonly HashSet<string> BlockedGitCommands = new(StringComparer.OrdinalIgnoreCase)
        { "reset", "clean", "reflog", "gc", "prune" };

    public async Task<ActionResult> RunAsync(
        string program,
        IReadOnlyList<string> arguments,
        string workingDirectory,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        if (program.IndexOfAny([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar]) >= 0 ||
            !AllowedPrograms.Contains(program))
        {
            return ActionResult.Fail(ErrorCodes.CommandNotAllowed, "Program is not in the command whitelist.");
        }

        if (program.StartsWith("git", StringComparison.OrdinalIgnoreCase) && IsUnsafeGit(arguments))
        {
            return ActionResult.Fail(ErrorCodes.CommandNotAllowed, "Destructive Git commands and force options are blocked.");
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = program,
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        var stopwatch = Stopwatch.StartNew();
        try
        {
            using var process = new Process { StartInfo = startInfo };
            if (!process.Start())
            {
                return ActionResult.Fail(ErrorCodes.ExecutionFailed, "Process could not be started.");
            }

            var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
            var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);
            using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutSource.CancelAfter(timeout);
            try
            {
                await process.WaitForExitAsync(timeoutSource.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                process.Kill(true);
                await process.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false);
                return ActionResult.Fail(ErrorCodes.CommandTimeout, $"Command exceeded {timeout.TotalSeconds:0.#} seconds.");
            }

            var stdout = await stdoutTask.ConfigureAwait(false);
            var stderr = await stderrTask.ConfigureAwait(false);
            stopwatch.Stop();
            return ActionResult.Ok(new { stdout, stderr, exitCode = process.ExitCode, durationMs = stopwatch.ElapsedMilliseconds });
        }
        catch (Exception exception) when (exception is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            return ActionResult.Fail(ErrorCodes.ExecutionFailed, exception.Message);
        }
    }

    private static bool IsUnsafeGit(IReadOnlyList<string> arguments)
    {
        if (arguments.Count == 0)
        {
            return false;
        }

        if (BlockedGitCommands.Contains(arguments[0]))
        {
            return true;
        }

        return arguments.Any(argument => argument.Equals("--force", StringComparison.OrdinalIgnoreCase) ||
                                         argument.StartsWith("--force=", StringComparison.OrdinalIgnoreCase) ||
                                         argument.Equals("-f", StringComparison.OrdinalIgnoreCase));
    }
}
