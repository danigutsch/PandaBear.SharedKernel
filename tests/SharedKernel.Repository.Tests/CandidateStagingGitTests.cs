namespace PandaBear.Repository.Tests;

/// <summary>Tests stage-candidate against real Git repositories.</summary>
public sealed class CandidateStagingGitTests
{
    [Fact]
    public async Task Stage_candidate_reads_the_library_checkout_without_inherited_git_variables()
    {
        // Arrange
        using var scope = new CandidateStagingScope();
        var revision = await TestGit.CommitAll(scope.LibraryRoot, TestContext.Current.CancellationToken);
        var decoy = Directory.CreateDirectory(Path.Combine(scope.Base, "decoy")).FullName;
        await File.WriteAllTextAsync(Path.Combine(decoy, "other.txt"), "other", TestContext.Current.CancellationToken);
        await TestGit.CommitAll(decoy, TestContext.Current.CancellationToken);
        var pin = scope.CreatePin();
        pin["libraryRevision"] = revision;
        scope.WritePin(pin);

        // Git honors GIT_DIR and GIT_WORK_TREE from the environment, which would point every command at the decoy.
        var environment = new Dictionary<string, string?>
        {
            ["GIT_DIR"] = Path.Combine(decoy, ".git"),
            ["GIT_WORK_TREE"] = decoy,
        };

        // Act
        var result = await CiToolProcess.Run(scope.Base, scope.Arguments(), environment, TestContext.Current.CancellationToken);

        // Assert
        result.ExitCode.ShouldBe(0, result.Error);
        File.Exists(Path.Combine(scope.Output, "candidate.json")).ShouldBeTrue();
    }

    [Theory]
    [InlineData("subdirectory", "is not the top level of a Git work tree")]
    [InlineData("untracked", "has uncommitted changes")]
    [InlineData("modified", "has uncommitted changes")]
    public async Task Stage_candidate_rejects_a_library_checkout_that_is_not_a_clean_top_level(string state, string expectedError)
    {
        // Arrange
        using var scope = new CandidateStagingScope();
        var subdirectory = Directory.CreateDirectory(Path.Combine(scope.LibraryRoot, "tools")).FullName;
        await File.WriteAllTextAsync(Path.Combine(subdirectory, "global.json"), CandidateStagingScope.GlobalJson, TestContext.Current.CancellationToken);
        var revision = await TestGit.CommitAll(scope.LibraryRoot, TestContext.Current.CancellationToken);
        var pin = scope.CreatePin();
        pin["libraryRevision"] = revision;
        scope.WritePin(pin);
        var arguments = scope.Arguments();
        switch (state)
        {
            case "subdirectory":
                arguments[Array.IndexOf(arguments, "--library-root") + 1] = subdirectory;
                break;
            case "untracked":
                await File.WriteAllTextAsync(Path.Combine(scope.LibraryRoot, "untracked.txt"), "new", TestContext.Current.CancellationToken);
                break;
            default:
                await File.AppendAllTextAsync(Path.Combine(subdirectory, "global.json"), "\n", TestContext.Current.CancellationToken);
                break;
        }

        // Act
        var result = await CiToolProcess.Run(scope.Base, arguments, new Dictionary<string, string?>(), TestContext.Current.CancellationToken);

        // Assert
        result.ExitCode.ShouldBe(2);
        result.Error.ShouldContain(expectedError, StringComparison.Ordinal);
        Directory.Exists(scope.Output).ShouldBeFalse();
    }
}
