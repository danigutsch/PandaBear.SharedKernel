namespace PandaBear.Testing.Tests;

/// <summary>Tests the walk up from a test's output directory to the repository root.</summary>
public sealed class RepositoryRootTests
{
    [Fact]
    public void Locate_returns_the_nearest_directory_at_or_above_the_start_that_holds_the_marker()
    {
        // Arrange
        using var directory = new TemporaryDirectory();
        var nested = Directory.CreateDirectory(Path.Combine(directory.Path, "outer", "inner", "bin")).FullName;
        File.WriteAllText(Path.Combine(directory.Path, "Root.slnx"), string.Empty);
        File.WriteAllText(Path.Combine(directory.Path, "outer", "Root.slnx"), string.Empty);

        // Act
        var root = RepositoryRoot.Locate("Root.slnx", nested);

        // Assert
        root.ShouldBe(Path.Combine(directory.Path, "outer"));
    }

    [Fact]
    public void Locate_accepts_the_start_directory_itself_and_a_relative_marker_path()
    {
        // Arrange
        using var directory = new TemporaryDirectory();
        Directory.CreateDirectory(Path.Combine(directory.Path, "src"));
        File.WriteAllText(Path.Combine(directory.Path, "src", "Directory.Build.props"), string.Empty);

        // Act
        var root = RepositoryRoot.Locate(Path.Combine("src", "Directory.Build.props"), directory.Path);

        // Assert
        root.ShouldBe(directory.Path);
    }

    [Fact]
    public void Locate_names_the_marker_and_the_start_directory_when_no_directory_holds_the_marker()
    {
        // Arrange
        using var directory = new TemporaryDirectory();
        var marker = "Missing-" + Guid.NewGuid().ToString("N") + ".slnx";

        Func<object?> locate = () => RepositoryRoot.Locate(marker, directory.Path);

        // Act
        var exception = locate.ShouldThrow<DirectoryNotFoundException>();

        // Assert
        exception.Message.ShouldBe($"{marker} was not found in {directory.Path} or any directory above it.");
    }

    [Fact]
    public void TryLocate_returns_null_when_no_directory_holds_the_marker()
    {
        // Arrange
        using var directory = new TemporaryDirectory();

        // Act
        var root = RepositoryRoot.TryLocate("Missing-" + Guid.NewGuid().ToString("N") + ".slnx", directory.Path);

        // Assert
        root.ShouldBeNull();
    }

    [Fact]
    public void Locate_starts_at_the_test_base_directory_by_default()
    {
        // Act
        var root = RepositoryRoot.Locate("SharedKernel.slnx");

        // Assert
        File.Exists(Path.Combine(root, "SharedKernel.slnx")).ShouldBeTrue();
        Path.GetRelativePath(root, AppContext.BaseDirectory).StartsWith("..", StringComparison.Ordinal).ShouldBeFalse();
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void Locate_rejects_a_blank_marker(string marker)
    {
        // Arrange
        Func<object?> locate = () => RepositoryRoot.Locate(marker);

        // Act
        var exception = locate.ShouldThrow<ArgumentException>();

        // Assert
        exception.ParamName.ShouldBe("marker");
    }
}
