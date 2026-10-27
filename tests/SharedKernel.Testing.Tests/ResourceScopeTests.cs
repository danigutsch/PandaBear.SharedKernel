namespace PandaBear.Testing.Tests;

[Collection("Environment variables")]
/// <summary>Tests the disposable test scopes that temporarily set environment variables and create temporary directories.</summary>
public sealed class ResourceScopeTests
{
    [Fact]
    public void Environment_scope_restores_unset_and_nested_values_after_failure()
    {
        // Arrange
        var name = $"PANDABEAR_SCOPE_{Guid.NewGuid():N}";
        string? valueInInnerScope = "unobserved";

        // Act
        var valueBeforeScope = Environment.GetEnvironmentVariable(name);
        string? valueInOuterScope;
        string? valueAfterInnerFailure;
        {
            using var outer = new EnvironmentVariableScope(name, "outer");
            valueInOuterScope = Environment.GetEnvironmentVariable(name);
            Action action = () =>
            {
                using var inner = new EnvironmentVariableScope(name, null);
                valueInInnerScope = Environment.GetEnvironmentVariable(name);
                throw new InvalidOperationException("test");
            };
            action.ShouldThrow<InvalidOperationException>();
            valueAfterInnerFailure = Environment.GetEnvironmentVariable(name);
        }

        var valueAfterScope = Environment.GetEnvironmentVariable(name);

        // Assert
        valueBeforeScope.ShouldBeNull();
        valueInOuterScope.ShouldBe("outer");
        valueInInnerScope.ShouldBeNull();
        valueAfterInnerFailure.ShouldBe("outer");
        valueAfterScope.ShouldBeNull();
    }

    [Fact]
    public void Temporary_directory_removes_nested_readonly_files_and_allows_repeated_disposal()
    {
        // Arrange
        using var directory = new TemporaryDirectory();
        var path = directory.Path;
        var child = Directory.CreateDirectory(Path.Combine(path, "nested"));
        var file = Path.Combine(child.FullName, "object");
        File.WriteAllText(file, "data");
        File.SetAttributes(file, File.GetAttributes(file) | FileAttributes.ReadOnly);

        // Act
        directory.Dispose();
        var directoryExists = Directory.Exists(path);
        directory.Dispose();

        // Assert
        directoryExists.ShouldBeFalse();
    }

    [Fact]
    public void Temporary_directory_does_not_traverse_symbolic_links()
    {
        // Arrange
        using var target = new TemporaryDirectory();
        var file = Path.Combine(target.Path, "preserved");
        File.WriteAllText(file, "data");
        using var directory = new TemporaryDirectory();
        Directory.CreateSymbolicLink(Path.Combine(directory.Path, "link"), target.Path);

        // Act
        directory.Dispose();

        // Assert
        File.ReadAllText(file).ShouldBe("data");
    }
}
