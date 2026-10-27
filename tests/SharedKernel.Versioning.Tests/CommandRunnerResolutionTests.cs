using PandaBear.Testing;

namespace PandaBear.Versioning.Tests;

/// <summary>
/// Tests how the versioning tool resolves the git executable.
/// </summary>
[Trait(SharedKernelTestTraitNames.CapabilityName, TestTraits.VersioningCapability)]
public static class CommandRunnerResolutionTests
{
    [Theory]
    [InlineData("git")]
    [InlineData("relative-bin/git")]
    public static async Task Tool_ignores_git_in_the_working_directory_and_relative_path_entries(string fakeGit)
    {
        // Arrange
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        // Git runs in the repository root, so the tool runs there too: a relative PATH entry then names the same file for both.
        using var directory = new TemporaryDirectory();
        var marker = Path.Combine(directory.Path, "fake-git-ran");
        FakeExecutable.WriteMarker(Path.Combine(directory.Path, fakeGit), marker);
        var path = "relative-bin" + Path.PathSeparator + Environment.GetEnvironmentVariable("PATH");

        // Act
        var result = await VersioningToolProcess.Run(
            directory.Path,
            ["calculate-release", "--run-number", "1", "--repo-root", directory.Path],
            path,
            TestContext.Current.CancellationToken);

        // Assert
        result.ExitCode.ShouldBe(2, "The directory is not a Git repository, so the real git fails.");
        File.Exists(marker).ShouldBeFalse("A bare git resolves only from absolute PATH entries, never from the working directory.");
    }
}
