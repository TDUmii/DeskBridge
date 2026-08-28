using DeskBridge.Core.Actions;
using DeskBridge.Core.Models;

namespace DeskBridge.Tests;

public sealed class CommandRunnerTests
{
    [Theory]
    [InlineData("powershell")]
    [InlineData("powershell.exe")]
    [InlineData("pwsh")]
    [InlineData("cmd")]
    [InlineData("cmd.exe")]
    [InlineData("unknown.exe")]
    public async Task BlocksNonWhitelistedPrograms(string program)
    {
        using var workspace = new TestWorkspace();
        var result = await new CommandRunner().RunAsync(program, [], workspace.Root, TimeSpan.FromSeconds(1), CancellationToken.None);
        Assert.Equal(ErrorCodes.CommandNotAllowed, result.Error?.Code);
    }

    [Theory]
    [InlineData("reset")]
    [InlineData("clean")]
    [InlineData("push", "--force")]
    public async Task BlocksDestructiveGit(string first, string? second = null)
    {
        using var workspace = new TestWorkspace();
        var args = second is null ? new[] { first } : new[] { first, second };
        var result = await new CommandRunner().RunAsync("git", args, workspace.Root, TimeSpan.FromSeconds(1), CancellationToken.None);
        Assert.Equal(ErrorCodes.CommandNotAllowed, result.Error?.Code);
    }

    [Fact]
    public async Task AllowsGitStatus()
    {
        using var workspace = new TestWorkspace();
        var result = await new CommandRunner().RunAsync("git", ["status"], workspace.Root, TimeSpan.FromSeconds(10), CancellationToken.None);
        Assert.True(result.Success);
    }

    [Fact]
    public async Task TerminatesTimedOutProcessTree()
    {
        using var workspace = new TestWorkspace();
        var result = await new CommandRunner().RunAsync("node", ["-e", "setTimeout(() => {}, 5000)"],
            workspace.Root, TimeSpan.FromMilliseconds(100), CancellationToken.None);
        Assert.Equal(ErrorCodes.CommandTimeout, result.Error?.Code);
    }

    [Fact]
    public async Task ReportsInvalidWorkingDirectory()
    {
        using var workspace = new TestWorkspace();
        var result = await new CommandRunner().RunAsync("git", ["status"], workspace.PathOf("missing"),
            TimeSpan.FromSeconds(1), CancellationToken.None);
        Assert.Equal(ErrorCodes.ExecutionFailed, result.Error?.Code);
    }
}
