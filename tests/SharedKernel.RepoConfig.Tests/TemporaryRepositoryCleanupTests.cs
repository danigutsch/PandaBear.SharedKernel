namespace SharedKernel.RepoConfig.Tests;

[Trait(TestTraitNames.CategoryName, TestTraits.CommandLineCategory)]
public sealed class TemporaryRepositoryCleanupTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Dispose_removes_nested_read_only_files(bool useGitRepository)
    {
        // Arrange
        using IDisposable repository = useGitRepository ? new TemporaryGitRepository() : new TemporaryCiTestRepository();
        var rootPath = repository switch
        {
            TemporaryGitRepository git => git.RootPath,
            TemporaryCiTestRepository ci => ci.RootPath,
            _ => throw new InvalidOperationException("Unexpected repository fixture.")
        };
        var objectsPath = Path.Combine(rootPath, ".git", "objects", "ab");
        Directory.CreateDirectory(objectsPath);
        var objectPath = Path.Combine(objectsPath, "read-only-object");
        File.WriteAllText(objectPath, "object content");
        File.SetAttributes(objectPath, File.GetAttributes(objectPath) | FileAttributes.ReadOnly);

        // Act
        repository.Dispose();

        // Assert
        Directory.Exists(rootPath).ShouldBeFalse();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Dispose_succeeds_when_the_root_is_already_removed(bool useGitRepository)
    {
        // Arrange
        using IDisposable repository = useGitRepository ? new TemporaryGitRepository() : new TemporaryCiTestRepository();
        var rootPath = repository switch
        {
            TemporaryGitRepository git => git.RootPath,
            TemporaryCiTestRepository ci => ci.RootPath,
            _ => throw new InvalidOperationException("Unexpected repository fixture.")
        };
        Directory.Delete(rootPath, recursive: true);

        // Act
        repository.Dispose();

        // Assert
        Directory.Exists(rootPath).ShouldBeFalse();
    }

    [Fact]
    public async Task DeleteLooseObject_removes_a_read_only_git_blob()
    {
        // Arrange
        using var repository = new TemporaryGitRepository();
        var cancellationToken = TestContext.Current.CancellationToken;
        await repository.Initialize(cancellationToken);
        repository.WriteText("blob.txt", "blob content");
        var objectId = await repository.StoreBlob("blob.txt", cancellationToken);
        var objectPath = Path.Combine(repository.RootPath, ".git", "objects", objectId[..2], objectId[2..]);
        File.SetAttributes(objectPath, File.GetAttributes(objectPath) | FileAttributes.ReadOnly);

        // Act
        repository.DeleteLooseObject(objectId);

        // Assert
        File.Exists(objectPath).ShouldBeFalse();
        File.Exists(Path.Combine(repository.RootPath, "blob.txt")).ShouldBeTrue();
    }

    [Fact]
    public async Task Dispose_removes_read_only_files_in_linked_worktrees()
    {
        // Arrange
        using var repository = new TemporaryGitRepository();
        var cancellationToken = TestContext.Current.CancellationToken;
        await repository.Initialize(cancellationToken);
        var commit = await CiTestSelectionTestProcess.Run(
            "git",
            repository.RootPath,
            ["-c", "user.name=Repo Config Tests", "-c", "user.email=repo-config-tests@example.invalid", "-c", "commit.gpgsign=false", "commit", "--allow-empty", "--message", "base"],
            cancellationToken);
        commit.ExitCode.ShouldBe(0);
        var linkedPath = await repository.CreateLinkedWorktree(cancellationToken);
        var filePath = Path.Combine(linkedPath, "read-only.txt");
        await File.WriteAllTextAsync(filePath, "linked content", cancellationToken);
        File.SetAttributes(filePath, File.GetAttributes(filePath) | FileAttributes.ReadOnly);

        // Act
        repository.Dispose();

        // Assert
        Directory.Exists(linkedPath).ShouldBeFalse();
        Directory.Exists(repository.RootPath).ShouldBeFalse();
    }
}
