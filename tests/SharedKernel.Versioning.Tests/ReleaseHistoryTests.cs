using PandaBear.Processes;

namespace PandaBear.Versioning.Tests;

/// <summary>Tests how the release history reads versions and breaking-change markers from Git.</summary>
[Trait(SharedKernelTestTraitNames.CapabilityName, TestTraits.VersioningCapability)]
public static class ReleaseHistoryTests
{
    [Fact]
    public static async Task Calculating_a_prerelease_reads_head_the_latest_tag_and_the_messages_since_it()
    {
        // Arrange
        var runner = new ScriptedProcessRunner(static arguments => arguments[0] switch
        {
            "rev-parse" => new ProcessOutput(0, "0123456789abcdef\n"),
            "describe" => new ProcessOutput(0, "v1.2.3\n"),
            _ => new ProcessOutput(0, "feat: add a thing\0"),
        });
        var history = new ReleaseHistory(runner, runner);

        // Act
        var result = await history.CalculateVersion(new ReleaseVersionOptions("/repo", "prerelease", "7"), TestContext.Current.CancellationToken);

        // Assert
        result.SourceTag.ShouldBe("v1.2.3");
        result.VersionOutput.SemVer.ShouldBe("1.3.0-alpha.7");
        runner.Started[2].ArgumentList.ShouldBe(["log", "--format=%B%x00", "v1.2.3..HEAD"]);
        runner.Started.ShouldAllSatisfy(static startInfo => startInfo.WorkingDirectory.ShouldBe("/repo"));
    }

    [Fact]
    public static async Task Calculating_without_a_tag_reads_the_whole_history_from_the_given_revision()
    {
        // Arrange
        var runner = new ScriptedProcessRunner(static arguments => arguments[0] == "describe"
            ? new ProcessOutput(128, string.Empty, "fatal: No names found")
            : new ProcessOutput(0, "fix: repair\0"));
        var history = new ReleaseHistory(runner, runner);

        // Act
        var result = await history.CalculateVersion(new ReleaseVersionOptions("/repo", "stable", Sha: "fedcba"), TestContext.Current.CancellationToken);

        // Assert
        result.SourceTag.ShouldBeEmpty();
        runner.Started.Select(static startInfo => startInfo.ArgumentList[0]).ShouldBe(["describe", "log"], "A given revision needs no rev-parse.");
        runner.Started[1].ArgumentList.ShouldBe(["log", "--format=%B%x00", "HEAD"]);
    }

    [Fact]
    public static async Task A_prerelease_with_a_blank_run_number_is_rejected_before_git_runs()
    {
        // Arrange
        var runner = new ScriptedProcessRunner(static _ => new ProcessOutput(0, string.Empty));
        var history = new ReleaseHistory(runner, runner);

        // Act
        Func<Task> action = () => history.CalculateVersion(new ReleaseVersionOptions("/repo", "prerelease", " "), TestContext.Current.CancellationToken);

        // Assert
        await action.ShouldThrow<ArgumentException>();
        runner.Started.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("feat(api)!: remove route\0", true)]
    [InlineData("fix: repair\0", false)]
    public static async Task The_breaking_change_marker_is_read_from_the_range_after_the_end_of_options(string log, bool expected)
    {
        // Arrange
        var runner = new ScriptedProcessRunner(_ => new ProcessOutput(0, log));
        var history = new ReleaseHistory(runner, runner);

        // Act
        var found = await history.HasBreakingChangeMarker("origin/main..HEAD", "/repo", TestContext.Current.CancellationToken);

        // Assert
        found.ShouldBe(expected);
        runner.Started.ShouldHaveSingleItem().ArgumentList.ShouldBe(["log", "--format=%B%x00", "--end-of-options", "origin/main..HEAD"]);
    }
}
