namespace PandaBear.Repository.Tests;

/// <summary>Guards how the CI tool finds the <c>git</c> executable.</summary>
public sealed class ExecutableResolutionTests
{
    [Theory]
    [InlineData("git")]
    [InlineData("relative-bin/git")]
    public async Task Tool_ignores_git_in_the_working_directory_and_relative_path_entries(string fakeGit)
    {
        // Arrange
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        // Git runs in the root, so the tool runs there too: a relative PATH entry then names the same file for both.
        using var repository = new RepositoryTestScope();
        var marker = Path.Combine(repository.Root, "fake-git-ran");
        FakeExecutable.WriteMarker(Path.Combine(repository.Root, fakeGit), marker);
        var environment = new Dictionary<string, string?>
        {
            ["PATH"] = "relative-bin" + Path.PathSeparator + Environment.GetEnvironmentVariable("PATH"),
        };

        // Act
        var result = await CiToolProcess.Run(repository.Root, ["select"], environment, TestContext.Current.CancellationToken);

        // Assert
        result.ExitCode.ShouldBe(0, "Selection without a readable revision still selects every project.");
        File.Exists(marker).ShouldBeFalse("A bare git resolves only from absolute PATH entries, never from the working directory.");
    }
}
