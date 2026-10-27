using PandaBear.Tooling;

namespace PandaBear.RepoConfig.Tests;

/// <summary>Tests how the resolver finds executables on the search path.</summary>
public sealed class ExecutableResolverTests
{
    /// <summary>Verifies that the resolver returns an existing executable found in an absolute path entry.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Resolve_returns_an_existing_executable_in_an_absolute_path_entry(bool isWindows)
    {
        // Arrange
        using var directory = new TemporaryDirectory();
        var expectedPath = ExecutableResolverTestFiles.WriteExecutable(directory.Path, isWindows ? "git.exe" : "git");

        // Act
        var resolvedPath = ExecutableResolver.Resolve("git", directory.Path, isWindows);

        // Assert
        resolvedPath.ShouldBe(expectedPath);
    }

    /// <summary>Verifies that the resolver skips relative path entries, even when they contain a match.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Resolve_ignores_relative_path_entries(bool isWindows)
    {
        // Arrange
        using var directory = new TemporaryDirectory();
        var expectedPath = ExecutableResolverTestFiles.WriteExecutable(directory.Path, isWindows ? "git.exe" : "git");
        var relativeDirectory = Path.GetRelativePath(Environment.CurrentDirectory, directory.Path);
        var path = $"{relativeDirectory}{Path.PathSeparator}{directory.Path}";

        // Act
        var resolvedPath = ExecutableResolver.Resolve("git", path, isWindows);

        // Assert
        resolvedPath.ShouldBe(expectedPath, "Only the absolute entry may supply the executable.");
    }

    /// <summary>Verifies that the resolver finds nothing when every path entry is relative.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Resolve_rejects_a_path_with_only_relative_entries(bool isWindows)
    {
        // Act
        var resolvedPath = ExecutableResolver.Resolve("git", "relative", isWindows);

        // Assert
        resolvedPath.ShouldBeNull();
    }

    /// <summary>Verifies that on Unix a file counts as executable only when an execute permission bit is set.</summary>
    [Theory]
    [InlineData(UnixFileMode.UserRead, false)]
    [InlineData(UnixFileMode.UserExecute, true)]
    [InlineData(UnixFileMode.GroupExecute, true)]
    [InlineData(UnixFileMode.OtherExecute, true)]
    public void Unix_execute_permission_requires_an_execute_bit(UnixFileMode mode, bool expected)
    {
        // Act
        var hasExecutePermission = ExecutableResolver.HasUnixExecutePermission(mode);

        // Assert
        hasExecutePermission.ShouldBe(expected);
    }
}
