using PandaBear.IntegrationTesting;

namespace PandaBear.Npgsql.Tests;

/// <summary>Tests TestWorkspace, the private temporary folder that a test run's files live in and that is removed afterwards.</summary>
public sealed class TestWorkspaceTests
{
    [Fact]
    public async Task Folders_are_created_inside_the_workspace_and_removed_with_it()
    {
        // Arrange
        var workspace = TestWorkspace.Create("sk-workspace");
        var folder = workspace.Folder(Path.Combine("data", "media"));
        await File.WriteAllTextAsync(Path.Combine(folder, "file.txt"), "content", TestContext.Current.CancellationToken);
        var root = workspace.Root;

        // Act
        await workspace.DisposeAsync();

        // Assert
        folder.ShouldBe(Path.Combine(root, "data", "media"));
        Directory.Exists(root).ShouldBeFalse("disposal must remove the workspace and everything in it");
    }

    [Fact]
    public async Task Two_workspaces_never_share_a_folder()
    {
        // Arrange
        await using var first = TestWorkspace.Create("sk-workspace");
        await using var second = TestWorkspace.Create("sk-workspace");

        // Act
        var roots = new[] { first.Root, second.Root };

        // Assert
        roots.Distinct(StringComparer.Ordinal).Count().ShouldBe(2);
        roots.ShouldAllSatisfy(root => Path.IsPathFullyQualified(root).ShouldBeTrue(), "a workspace is a full path");
    }

    [Theory]
    [InlineData("../outside")]
    [InlineData("/etc")]
    [InlineData(" ")]
    public async Task A_folder_that_leaves_the_workspace_is_refused(string relativePath)
    {
        // Arrange
        await using var workspace = TestWorkspace.Create("sk-workspace");
        Action folder = () => workspace.Folder(relativePath);

        // Act
        var refused = folder.ShouldThrow<ArgumentException>();

        // Assert
        refused.ParamName.ShouldBe("relativePath");
    }

    [Fact]
    public async Task The_release_callback_runs_once_before_the_folder_is_removed()
    {
        // Arrange
        var calls = 0;
        var folderExistedDuringRelease = false;
        TestWorkspace? workspace = null;
        workspace = TestWorkspace.Create("sk-workspace", releaseFiles: () =>
        {
            calls++;
            folderExistedDuringRelease = Directory.Exists(workspace!.Root);
        });

        // Act
        await workspace.DisposeAsync();
        await workspace.DisposeAsync();

        // Assert
        calls.ShouldBe(1);
        folderExistedDuringRelease.ShouldBeTrue("pooled SQLite connections must be released while their files still exist, before the delete");
        Directory.Exists(workspace.Root).ShouldBeFalse();
    }

    [Fact]
    public async Task Disposing_twice_is_harmless()
    {
        // Arrange
        var workspace = TestWorkspace.Create("sk-workspace");
        await workspace.DisposeAsync();

        // Act
        await workspace.DisposeAsync();

        // Assert
        Directory.Exists(workspace.Root).ShouldBeFalse();
    }
}
