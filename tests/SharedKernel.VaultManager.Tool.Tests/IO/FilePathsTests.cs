using PandaBear.VaultManager.Tool.IO;
using PandaBear.VaultManager.Tool.Tests.Pipeline;

namespace PandaBear.VaultManager.Tool.Tests.IO;

public sealed class FilePathsTests : IDisposable
{
    private readonly string directory = Directory.CreateTempSubdirectory("vault-file-paths-").FullName;

    public void Dispose() => Directory.Delete(directory, recursive: true);

    [Theory(SkipType = typeof(FileSystemProbes), SkipUnless = nameof(FileSystemProbes.CanCreateSymbolicLinks), Skip = "This system does not let the test create symbolic links.")]
    [InlineData(false)]
    [InlineData(true)]
    public void MayNameSameFile_applies_a_parent_step_in_a_link_target_to_the_directory_the_link_before_it_reaches(bool absoluteTarget)
    {
        // Arrange
        string exports = Directory.CreateDirectory(Path.Combine(directory, "e", "exports")).FullName;
        string nested = Directory.CreateDirectory(Path.Combine(directory, "e", "f")).FullName;
        string links = Directory.CreateDirectory(Path.Combine(directory, "d")).FullName;
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
        string exports = Directory.CreateDirectory(Path.Combine(directory, "exports")).FullName;
        string input = Path.Combine(exports, "bitwarden.json");
        File.WriteAllText(input, "export");
        File.CreateSymbolicLink(Path.Combine(directory, "export-link.json"), Path.Join(".", "exports", "bitwarden.json"));
        Directory.CreateSymbolicLink(Path.Combine(directory, "alias"), Path.Join(".", "exports", "."));

        // Act
        bool throughFileLink = FilePaths.MayNameSameFile(input, Path.Combine(directory, "export-link.json"));
        bool throughDirectoryLink = FilePaths.MayNameSameFile(input, Path.Join(directory, "alias", ".", "bitwarden.json"));
        bool otherFile = FilePaths.MayNameSameFile(input, Path.Combine(directory, "alias", "other.json"));

        // Assert
        throughFileLink.ShouldBeTrue();
        throughDirectoryLink.ShouldBeTrue();
        otherFile.ShouldBeFalse();
    }

    [Fact(SkipType = typeof(FileSystemProbes), SkipUnless = nameof(FileSystemProbes.CanCreateSymbolicLinks), Skip = "This system does not let the test create symbolic links.")]
    public void ResolveLinks_rejects_links_that_form_a_loop()
    {
        // Arrange
        Directory.CreateSymbolicLink(Path.Combine(directory, "first"), Path.Combine(directory, "second"));
        Directory.CreateSymbolicLink(Path.Combine(directory, "second"), Path.Combine(directory, "first"));
        var action = () => FilePaths.ResolveLinks(Path.Combine(directory, "first", "merged.json"));

        // Act
        var exception = action.ShouldThrow<IOException>();

        // Assert
        exception.Message.ShouldContain("symbolic links", StringComparison.Ordinal);
    }
}
