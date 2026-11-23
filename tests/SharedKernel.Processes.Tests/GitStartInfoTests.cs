namespace PandaBear.Processes.Tests;

/// <summary>Tests the description of a git run that cannot prompt and receives no credentials.</summary>
[Trait(TestTraitNames.CategoryName, TestTraitValues.SecurityCategory)]
[Trait(TestTraitNames.ScopeName, SharedKernelTestTraitNames.UnitScope)]
[Collection(GitEnvironmentGroup.Name)]
public sealed class GitStartInfoTests
{
    [Fact]
    public void Git_is_the_absolute_path_the_resolver_finds_and_each_argument_is_passed_alone_without_a_shell()
    {
        // Arrange
        using var directory = new TemporaryDirectory();
        var git = PathExecutableResolverTestFiles.WriteExecutable(directory.Path, "git");
        var resolver = new PathExecutableResolver(directory.Path, isWindows: false);

        // Act
        var startInfo = GitStartInfo.Create(directory.Path, ["log", "--format=%H %s", "--", "a b"], resolver);

        // Assert
        startInfo.FileName.ShouldBe(git);
        startInfo.ArgumentList.ToArray().ShouldBe(["log", "--format=%H %s", "--", "a b"]);
        startInfo.UseShellExecute.ShouldBeFalse();
        startInfo.RedirectStandardOutput.ShouldBeTrue();
        startInfo.RedirectStandardError.ShouldBeTrue();
        startInfo.WorkingDirectory.ShouldBe(directory.Path);
    }

    [Fact]
    public void Tokens_askpass_programs_and_git_variables_are_removed_and_prompts_are_off()
    {
        // Arrange
        using var directory = new TemporaryDirectory();
        PathExecutableResolverTestFiles.WriteExecutable(directory.Path, "git");
        using var environment = new HostileGitEnvironment();

        // Act
        var startInfo = GitStartInfo.Create(directory.Path, ["status"], new PathExecutableResolver(directory.Path, isWindows: false));

        // Assert
        HostileGitEnvironment.ShouldNotReach([.. startInfo.Environment.Select(static variable => variable.Key + "=" + variable.Value)]);
        startInfo.Environment.ContainsKey("PATH").ShouldBeTrue("Other variables are kept.");
    }

    [Fact]
    public void Start_information_a_caller_built_gets_the_same_environment_rules()
    {
        // Arrange
        var startInfo = new System.Diagnostics.ProcessStartInfo("git");
        startInfo.Environment.Clear();
        startInfo.Environment["GH_TOKEN"] = "credential-marker";
        startInfo.Environment["GIT_ASKPASS"] = "/nonexistent/askpass";
        startInfo.Environment["SSH_ASKPASS"] = "/nonexistent/askpass";
        startInfo.Environment["git_dir"] = "/elsewhere";
        startInfo.Environment["GIT_TERMINAL_PROMPT"] = "1";
        startInfo.Environment["HOME"] = "/home/user";

        // Act
        GitStartInfo.ApplyEnvironment(startInfo);

        // Assert
        startInfo.Environment.Keys.Order(StringComparer.Ordinal).ToArray().ShouldBe(["GIT_TERMINAL_PROMPT", "HOME"]);
        startInfo.Environment["GIT_TERMINAL_PROMPT"].ShouldBe("0");
    }

    [Fact]
    public async Task A_git_started_from_the_description_receives_none_of_the_hosts_tokens_askpass_programs_or_git_variables()
    {
        // Arrange
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        using var directory = new TemporaryDirectory();
        var git = new RecordingFakeExecutable(directory.Path, "git", 0, string.Empty);
        using var environment = new HostileGitEnvironment();
        var startInfo = GitStartInfo.Create(directory.Path, ["status"], new PathExecutableResolver(directory.Path, isWindows: false));

        // Act
        var result = await new BoundedProcessRunner().Capture(startInfo, ReadOnlyMemory<byte>.Empty, 1024, 1024, TestContext.Current.CancellationToken);

        // Assert
        result.ExitCode.ShouldBe(0);
        HostileGitEnvironment.ShouldNotReach(git.Environment());
        git.Arguments().ShouldBe(["status"]);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("git")]
    [InlineData("bin/git")]
    public void No_git_or_a_git_that_is_not_fully_qualified_is_refused(string? found)
    {
        // Arrange
        var resolver = new ExecutableMap(_ => found);

        Func<object?> create = () => GitStartInfo.Create("/tmp", ["status"], resolver);

        // Act
        var exception = create.ShouldThrow<InvalidOperationException>();

        // Assert
        exception.Message.ShouldBe("Could not resolve git to an absolute executable path.");
    }

    [Fact]
    public void The_default_resolver_searches_only_absolute_path_directories()
    {
        // Arrange
        var expected = PathExecutableResolver.Default.Find("git");
        if (expected is null)
        {
            return;
        }

        // Act
        var startInfo = GitStartInfo.Create(".", ["status"]);

        // Assert
        startInfo.FileName.ShouldBe(expected);
        startInfo.WorkingDirectory.ShouldBe(Path.GetFullPath("."));
    }

    [Fact]
    public void Every_git_call_gets_thirty_seconds()
    {
        // Act
        var timeout = GitStartInfo.Timeout;

        // Assert
        timeout.ShouldBe(TimeSpan.FromSeconds(30));
    }
}
