using SharedKernel.Testing;

namespace SharedKernel.Versioning.Tests;

[Trait(SharedKernelTestTraitNames.CapabilityName, TestTraits.VersioningCapability)]
public sealed class TemporaryGitRepositoryTests
{
    [Fact]
    public void Dispose_removes_read_only_git_objects_and_can_be_called_twice()
    {
        // Arrange
        using var repository = new TemporaryGitRepository();
        repository.Commit("test.txt", "content", "feat: add content");
        var objects = Directory.GetFiles(Path.Combine(repository.Root, ".git", "objects"), "*", SearchOption.AllDirectories);
        objects.ShouldNotBeEmpty();
        foreach (var path in objects)
        {
            File.SetAttributes(path, File.GetAttributes(path) | FileAttributes.ReadOnly);
        }

        // Act
        repository.Dispose();
        repository.Dispose();

        // Assert
        Directory.Exists(repository.Root).ShouldBeFalse();
    }
}
