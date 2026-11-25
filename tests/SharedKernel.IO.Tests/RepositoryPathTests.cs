namespace SharedKernel.IO.Tests;

/// <summary>Tests validation and resolution of repository-relative paths.</summary>
public sealed class RepositoryPathTests
{
    [Theory]
    [InlineData("docs/README.md")]
    [InlineData("a")]
    [InlineData(".config/sharedkernel-ci.json")]
    public void Relative_paths_with_forward_slashes_are_valid(string value)
    {
        // Act
        var parsed = RepositoryPath.TryParse(value, out var path);

        // Assert
        parsed.ShouldBeTrue();
        path.Value.ShouldBe(value);
        path.ToString().ShouldBe(value);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("/etc/passwd")]
    [InlineData("docs\\README.md")]
    [InlineData("C:/Windows")]
    [InlineData("docs//README.md")]
    [InlineData("docs/./README.md")]
    [InlineData("docs/../secrets")]
    [InlineData("docs/")]
    public void Rooted_empty_dotted_or_backslashed_paths_are_invalid(string? value)
    {
        // Act
        var valid = RepositoryPath.IsValid(value);
        var parsed = RepositoryPath.TryParse(value, out var path);
        Action parse = () => RepositoryPath.Parse(value);

        // Assert
        valid.ShouldBeFalse();
        parsed.ShouldBeFalse();
        path.ShouldBe(default);
        parse.ShouldThrow<InvalidOperationException>();
    }

    [Fact]
    public void Resolve_under_combines_the_segments_with_the_root()
    {
        // Arrange
        using var root = new TemporaryDirectory();

        // Act
        var resolved = RepositoryPath.Parse("docs/README.md").ResolveUnder(root.Path);

        // Assert
        resolved.ShouldBe(Path.Combine(root.Path, "docs", "README.md"));
    }

    [Fact]
    public void Resolve_under_refuses_a_symbolic_link_in_the_path()
    {
        // Arrange
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        using var root = new TemporaryDirectory();
        using var outside = new TemporaryDirectory();
        Directory.CreateSymbolicLink(Path.Combine(root.Path, "linked"), outside.Path);

        // Act
        Action resolve = () => RepositoryPath.Parse("linked/file.txt").ResolveUnder(root.Path);

        // Assert
        resolve.ShouldThrow<InvalidOperationException>().Message.ShouldBe("Symbolic links are not followed in repository paths.");
    }

    [Fact]
    public void The_default_value_cannot_be_resolved()
    {
        // Act
        Action resolve = () => default(RepositoryPath).ResolveUnder(Path.GetTempPath());

        // Assert
        resolve.ShouldThrow<InvalidOperationException>();
    }
}
