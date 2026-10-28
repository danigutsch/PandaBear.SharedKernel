using PandaBear.Testing;
using PandaBear.VaultManager.Tool.IO;

namespace PandaBear.VaultManager.Tool.Tests.IO;

/// <summary>Tests the detection of two paths that name the same file through symbolic links.</summary>
public sealed class FilePathsTests : IDisposable
{
    private readonly TemporaryDirectory temporaryDirectory = new();

    private string DirectoryPath => temporaryDirectory.Path;

    /// <inheritdoc />
    public void Dispose() => temporaryDirectory.Dispose();

    [Theory(SkipType = typeof(FileSystemProbes), SkipUnless = nameof(FileSystemProbes.CanCreateSymbolicLinks), Skip = "This system does not let the test create symbolic links.")]
    [InlineData(false)]
    [InlineData(true)]
    public void MayNameSameFile_applies_a_parent_step_in_a_link_target_to_the_directory_the_link_before_it_reaches(bool absoluteTarget)
    {
        // Arrange
        string exports = Directory.CreateDirectory(Path.Combine(DirectoryPath, "e", "exports")).FullName;
        string nested = Directory.CreateDirectory(Path.Combine(DirectoryPath, "e", "f")).FullName;
        string links = Directory.CreateDirectory(Path.Combine(DirectoryPath, "d")).FullName;
        string input = Path.Combine(exports, "bitwarden.json");
        File.WriteAllText(input, "export");
        Directory.CreateSymbolicLink(Path.Combine(links, "nested"), nested);
        string target = Path.Join("nested", "..", "exports");
        Directory.CreateSymbolicLink(Path.Combine(links, "alias"), absoluteTarget ? Path.Join(links, target) : target);
        string output = Path.Combine(links, "alias", "bitwarden.json");
        string contentThroughLink = File.ReadAllText(output);

        // Act
        bool same = FilePaths.MayNameSameFile(input, output);

        // Assert
        contentThroughLink.ShouldBe("export");
        same.ShouldBeTrue();
    }

    [Fact(SkipType = typeof(FileSystemProbes), SkipUnless = nameof(FileSystemProbes.CanCreateSymbolicLinks), Skip = "This system does not let the test create symbolic links.")]
    public void MayNameSameFile_follows_a_file_link_and_relative_links_with_dot_segments()
    {
        // Arrange
        string exports = Directory.CreateDirectory(Path.Combine(DirectoryPath, "exports")).FullName;
        string input = Path.Combine(exports, "bitwarden.json");
        File.WriteAllText(input, "export");
        File.CreateSymbolicLink(Path.Combine(DirectoryPath, "export-link.json"), Path.Join(".", "exports", "bitwarden.json"));
        Directory.CreateSymbolicLink(Path.Combine(DirectoryPath, "alias"), Path.Join(".", "exports", "."));

        // Act
        bool throughFileLink = FilePaths.MayNameSameFile(input, Path.Combine(DirectoryPath, "export-link.json"));
        bool throughDirectoryLink = FilePaths.MayNameSameFile(input, Path.Join(DirectoryPath, "alias", ".", "bitwarden.json"));
        bool otherFile = FilePaths.MayNameSameFile(input, Path.Combine(DirectoryPath, "alias", "other.json"));

        // Assert
        throughFileLink.ShouldBeTrue();
        throughDirectoryLink.ShouldBeTrue();
        otherFile.ShouldBeFalse();
    }

    [Fact(SkipType = typeof(FileSystemProbes), SkipUnless = nameof(FileSystemProbes.CanCreateSymbolicLinks), Skip = "This system does not let the test create symbolic links.")]
    public void ResolveLinks_rejects_links_that_form_a_loop()
    {
        // Arrange
        Directory.CreateSymbolicLink(Path.Combine(DirectoryPath, "first"), Path.Combine(DirectoryPath, "second"));
        Directory.CreateSymbolicLink(Path.Combine(DirectoryPath, "second"), Path.Combine(DirectoryPath, "first"));
        var action = () => FilePaths.ResolveLinks(Path.Combine(DirectoryPath, "first", "merged.json"));

        // Act
        var exception = action.ShouldThrow<IOException>();

        // Assert
        exception.Message.ShouldContain("symbolic links", StringComparison.Ordinal);
    }
}
