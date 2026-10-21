namespace PandaBear.Testing.Tests;

[Collection("Environment variables")]
public sealed class ResourceScopeTests
{
    [Fact]
    public void Environment_scope_restores_unset_and_nested_values_after_failure()
    {
        var name = $"PANDABEAR_SCOPE_{Guid.NewGuid():N}";
        Environment.GetEnvironmentVariable(name).ShouldBeNull();
        {
            using var outer = new EnvironmentVariableScope(name, "outer");
            Environment.GetEnvironmentVariable(name).ShouldBe("outer");
            Action action = () =>
            {
                using var inner = new EnvironmentVariableScope(name, null);
                Environment.GetEnvironmentVariable(name).ShouldBeNull();
                throw new InvalidOperationException("test");
            };
            action.ShouldThrow<InvalidOperationException>();
            Environment.GetEnvironmentVariable(name).ShouldBe("outer");
        }

        Environment.GetEnvironmentVariable(name).ShouldBeNull();
    }

    [Fact]
    public void Temporary_directory_removes_nested_readonly_files_and_allows_repeated_disposal()
    {
        using var directory = new TemporaryDirectory();
        var path = directory.Path;
        var child = Directory.CreateDirectory(Path.Combine(path, "nested"));
        var file = Path.Combine(child.FullName, "object");
        File.WriteAllText(file, "data");
        File.SetAttributes(file, File.GetAttributes(file) | FileAttributes.ReadOnly);
        directory.Dispose();
        Directory.Exists(path).ShouldBeFalse();
        directory.Dispose();
    }

    [Fact]
    public void Temporary_directory_does_not_traverse_symbolic_links()
    {
        using var target = new TemporaryDirectory();
        var file = Path.Combine(target.Path, "preserved");
        File.WriteAllText(file, "data");
        using var directory = new TemporaryDirectory();
        Directory.CreateSymbolicLink(Path.Combine(directory.Path, "link"), target.Path);
        directory.Dispose();
        File.ReadAllText(file).ShouldBe("data");
    }
}
