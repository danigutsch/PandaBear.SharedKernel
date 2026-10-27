using PandaBear.Testing;

namespace PandaBear.Versioning.Tests;

/// <summary>
/// Tests cleanup of the temporary Git repository fixture.
/// </summary>
[Trait(SharedKernelTestTraitNames.CapabilityName, TestTraits.VersioningCapability)]
public sealed class TemporaryGitRepositoryTests
{
    [Fact]
    public async Task Dispose_removes_read_only_git_objects_and_can_be_called_twice()
    {
        // Arrange
        using var repository = new TemporaryGitRepository();
        await repository.Initialize(TestContext.Current.CancellationToken);
        await repository.Commit("test.txt", "content", "feat: add content", TestContext.Current.CancellationToken);
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
