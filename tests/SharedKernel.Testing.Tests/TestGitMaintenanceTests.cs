namespace PandaBear.Testing.Tests;

/// <summary>
/// Tests that the git that test setup runs starts no background maintenance, which would keep rewriting
/// <c>.git/objects</c> after the command returned and race with the test that reads or deletes the repository.
/// </summary>
public sealed class TestGitMaintenanceTests
{
    [Fact]
    public async Task A_commit_starts_no_background_maintenance_even_when_the_repository_asks_for_it()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        using var directory = new TemporaryDirectory();
        var repository = Path.Combine(directory.Path, "repository");
        var trace = Path.Combine(directory.Path, "git-trace.log");
        Directory.CreateDirectory(repository);
        await TestGit.Run(repository, ["init", "--quiet"], ct);
        await TestGit.Run(repository, ["config", "maintenance.auto", "true"], ct);
        await TestGit.Run(repository, ["config", "gc.auto", "1"], ct);
        await File.WriteAllTextAsync(Path.Combine(repository, "content.txt"), "content", ct);
        await TestGit.Run(repository, ["add", "--all"], ct);

        // Act
        var result = await TestGit.TryRun(
            repository,
            ["-c", "user.name=Test Author", "-c", "user.email=test-author@example.invalid", "-c", "commit.gpgsign=false", "commit", "--quiet", "--message", "test: change"],
            ct,
            new Dictionary<string, string?>(StringComparer.Ordinal) { ["GIT_TRACE"] = trace });

        // Assert
        result.ExitCode.ShouldBe(0, result.StandardError);
        var traced = await File.ReadAllTextAsync(trace, ct);
        traced.ShouldContain("commit", StringComparison.Ordinal);
        traced.ShouldNotContain("maintenance run", StringComparison.Ordinal);
        traced.ShouldNotContain("gc --auto", StringComparison.Ordinal);
    }
}
