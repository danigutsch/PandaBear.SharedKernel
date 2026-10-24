using System.Diagnostics;
using System.Text;

namespace PandaBear.RepoConfig.Tests;

[Trait(TestTraitNames.CategoryName, TestTraits.CommandLineCategory)]
[Collection(RepoConfigToolEnvironmentGroup.Name)]
public sealed class RoadmapGitRepositoryTests
{
    [Fact]
    public async Task Git_queries_use_fixed_noninteractive_arguments_without_credentials()
    {
        // Arrange
        using var ghTokenScope = new EnvironmentVariableScope("GH_TOKEN", "credential-marker");
        using var gitHubTokenScope = new EnvironmentVariableScope("GITHUB_TOKEN", "credential-marker");
        ProcessStartInfo? captured = null;
        var repository = new RoadmapGitRepository((startInfo, _) =>
        {
            captured = startInfo;
            return Task.FromResult(new RoadmapGitCommandResult(
                0,
                Encoding.UTF8.GetBytes(RoadmapAutomationTestPlan.BaseSha + "\n"),
                string.Empty,
                OutputLimitExceeded: false,
                ErrorLimitExceeded: false));
        });

        // Act
        var head = await repository.ReadHead("/tmp/repository", TestContext.Current.CancellationToken);
        var startInfo = captured.ShouldNotBeNull();

        // Assert
        head.ShouldBe(RoadmapAutomationTestPlan.BaseSha);
        Path.IsPathFullyQualified(startInfo.FileName).ShouldBeTrue("A bare name would let .NET run a git from the application or current directory before PATH.");
        Path.GetFileNameWithoutExtension(startInfo.FileName).ShouldBe("git");
        startInfo.ArgumentList.ToArray().ShouldBe(["rev-parse", "--verify", "HEAD"]);
        startInfo.UseShellExecute.ShouldBeFalse();
        startInfo.RedirectStandardOutput.ShouldBeTrue();
        startInfo.RedirectStandardError.ShouldBeTrue();
        startInfo.Environment["GIT_TERMINAL_PROMPT"].ShouldBe("0");
        startInfo.Environment.ContainsKey("GH_TOKEN").ShouldBeFalse();
        startInfo.Environment.ContainsKey("GITHUB_TOKEN").ShouldBeFalse();
    }

    [Fact]
    public async Task Git_queries_start_nothing_when_no_absolute_path_entry_holds_git()
    {
        // Arrange
        // The current directory and an empty entry are the only PATH entries.
        using var pathScope = new EnvironmentVariableScope("PATH", "." + Path.PathSeparator);
        var started = false;
        var repository = new RoadmapGitRepository((_, _) =>
        {
            started = true;
            return Task.FromResult(new RoadmapGitCommandResult(
                0,
                Encoding.UTF8.GetBytes(RoadmapAutomationTestPlan.BaseSha + "\n"),
                string.Empty,
                OutputLimitExceeded: false,
                ErrorLimitExceeded: false));
        });

        // Act
        Func<Task> action = async () => _ = await repository.ReadHead("/tmp/repository", TestContext.Current.CancellationToken);
        var exception = await action.ShouldThrow<InvalidOperationException>();

        // Assert
        exception.Message.ShouldBe("Could not resolve git to an absolute executable path.");
        started.ShouldBeFalse("Without an absolute PATH entry for git, no bare name falls back to the current directory.");
    }

    [Fact]
    public async Task Git_errors_do_not_expose_untrusted_diagnostics()
    {
        // Arrange
        var repository = new RoadmapGitRepository((_, _) => Task.FromResult(new RoadmapGitCommandResult(
            1,
            [],
            "diagnostic-credential-marker",
            OutputLimitExceeded: false,
            ErrorLimitExceeded: false)));

        // Act
        Func<Task> action = async () => _ = await repository.ReadHead("/tmp/repository", TestContext.Current.CancellationToken);
        var exception = await action.ShouldThrow<InvalidOperationException>();

        // Assert
        exception.Message.ShouldBe("Roadmap automation Git inspection failed.");
        exception.Message.ShouldNotContain("diagnostic-credential-marker", StringComparison.Ordinal);
    }

    [Fact]
    public async Task Git_cancellation_is_preserved()
    {
        // Arrange
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        var repository = new RoadmapGitRepository((_, token) => Task.FromException<RoadmapGitCommandResult>(new OperationCanceledException(token)));

        // Act
        Func<Task> action = async () => _ = await repository.ReadHead("/tmp/repository", cancellation.Token);
        var exception = await action.ShouldThrowAssignableTo<OperationCanceledException>();

        // Assert
        exception.CancellationToken.ShouldBe(cancellation.Token);
    }
}
