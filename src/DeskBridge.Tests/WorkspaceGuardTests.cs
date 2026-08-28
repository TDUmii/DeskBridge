using DeskBridge.Core.Actions;
using DeskBridge.Core.Models;
using DeskBridge.Core.Security;

namespace DeskBridge.Tests;

public sealed class WorkspaceGuardTests
{
    [Fact]
    public void AllowsPathInsideWorkspace()
    {
        using var workspace = new TestWorkspace();
        Assert.Equal(workspace.PathOf("src", "main.cs"), workspace.Context.WorkspaceGuard.EnsureInside(workspace.PathOf("src", "main.cs"), false));
    }

    [Fact]
    public void BlocksOutsideTraversalAndSimilarPrefix()
    {
        using var workspace = new TestWorkspace();
        AssertViolation(() => workspace.Context.WorkspaceGuard.EnsureInside(Path.Combine(workspace.Root, "..", "outside.txt"), false));
        AssertViolation(() => workspace.Context.WorkspaceGuard.EnsureInside(workspace.Root + "-Evil" + Path.DirectorySeparatorChar + "file.txt", false));
    }

    [Theory]
    [InlineData("../evil.txt")]
    [InlineData("..\\evil.txt")]
    public void BlocksRelativeTraversal(string child)
    {
        using var workspace = new TestWorkspace();
        var error = Assert.Throws<DeskBridgeActionException>(() => workspace.Context.WorkspaceGuard.ResolveRelative(workspace.Root, child));
        Assert.Equal(ErrorCodes.ProjectPathInvalid, error.Code);
    }

    [Fact]
    public void BlocksAbsoluteChildPath()
    {
        using var workspace = new TestWorkspace();
        var error = Assert.Throws<DeskBridgeActionException>(() => workspace.Context.WorkspaceGuard.ResolveRelative(workspace.Root, workspace.PathOf("evil.txt")));
        Assert.Equal(ErrorCodes.ProjectPathInvalid, error.Code);
    }

    private static void AssertViolation(Action action)
    {
        var error = Assert.Throws<DeskBridgeActionException>(action);
        Assert.Equal(ErrorCodes.WorkspaceViolation, error.Code);
    }
}
