namespace SharedKernel.Processes.Tests;

/// <summary>Tests how the resolver finds executables in the absolute directories of a search path.</summary>
public sealed class PathExecutableResolverTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Find_returns_an_existing_executable_in_an_absolute_path_entry(bool isWindows)
    {
        // Arrange
        using var directory = new TemporaryDirectory();
        var expectedPath = PathExecutableResolverTestFiles.WriteExecutable(directory.Path, isWindows ? "git.exe" : "git");

        // Act
        var resolvedPath = new PathExecutableResolver(directory.Path, isWindows).Find("git");

        // Assert
        resolvedPath.ShouldBe(expectedPath);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Find_ignores_relative_path_entries(bool isWindows)
    {
        // Arrange
        using var directory = new TemporaryDirectory();
        var expectedPath = PathExecutableResolverTestFiles.WriteExecutable(directory.Path, isWindows ? "git.exe" : "git");
        var relativeDirectory = Path.GetRelativePath(Environment.CurrentDirectory, directory.Path);
        var path = $"{relativeDirectory}{Path.PathSeparator}{directory.Path}";

        // Act
        var resolvedPath = new PathExecutableResolver(path, isWindows).Find("git");

        // Assert
        resolvedPath.ShouldBe(expectedPath, "Only the absolute entry may supply the executable.");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Find_rejects_a_path_with_only_relative_entries(bool isWindows)
    {
        // Act
        var resolvedPath = new PathExecutableResolver("relative", isWindows).Find("git");

        // Assert
        resolvedPath.ShouldBeNull();
    }

    [Theory]
    [InlineData(UnixFileMode.UserRead, false)]
    [InlineData(UnixFileMode.UserExecute, true)]
    [InlineData(UnixFileMode.GroupExecute, true)]
    [InlineData(UnixFileMode.OtherExecute, true)]
    public void Unix_execute_permission_requires_an_execute_bit(UnixFileMode mode, bool expected)
    {
        // Act
        var hasExecutePermission = ExecutableSearch.HasUnixExecutePermission(mode);

        // Assert
        hasExecutePermission.ShouldBe(expected);
    }

    [Fact]
    public void Resolve_returns_a_fully_qualified_path_unchanged_without_searching()
    {
        // Arrange
        var path = Path.Combine(Path.GetTempPath(), "missing-tool");

        // Act
        var resolved = new PathExecutableResolver(null, OperatingSystem.IsWindows()).Resolve(path);

        // Assert
        resolved.ShouldBe(path);
    }

    [Fact]
    public void Resolve_fails_when_no_absolute_directory_contains_the_command()
    {
        // Act
        Action resolve = () => new PathExecutableResolver("relative", OperatingSystem.IsWindows()).Resolve("git");

        // Assert
        resolve.ShouldThrow<InvalidOperationException>().Message.ShouldBe("Failed to start git: no absolute PATH directory contains it.");
    }

    [Fact]
    public void Find_rejects_a_blank_command()
    {
        // Act
        Action find = () => new PathExecutableResolver("/usr/bin", isWindows: false).Find(" ");

        // Assert
        find.ShouldThrow<ArgumentException>();
    }
}
