namespace PandaBear.RepoConfig.Tests;

[Trait(TestTraitNames.CategoryName, TestTraits.CommandLineCategory)]
[Collection("Repo config tool environment")]
public sealed class ChildProcessEnvironmentTests
{
    [Fact]
    public async Task Verify_podman_dns_starts_processes_without_github_tokens()
    {
        // Arrange
        using var ghTokenScope = new EnvironmentVariableScope("GH_TOKEN", "credential-marker");
        using var gitHubTokenScope = new EnvironmentVariableScope("GITHUB_TOKEN", "credential-marker");
        using var context = new PodmanDnsPreflightTestContext
        {
            Networks = "trip\n",
            Containers = "container-id\n"
        };
        context.WriteConfig("trip");
        context.WriteConfig("trip%int");
        context.AddDaemon(1201);

        // Act
        var result = await context.Run();
        var executables = context.ProcessStartInfos
            .Select(startInfo => startInfo.FileName)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();

        // Assert
        result.ExitCode.ShouldBe(0);
        executables.ShouldBe(["/usr/bin/podman", "/usr/bin/stat"]);
        context.ProcessStartInfos.ShouldAllSatisfy(startInfo =>
        {
            startInfo.Environment.ContainsKey("GH_TOKEN").ShouldBeFalse();
            startInfo.Environment.ContainsKey("GITHUB_TOKEN").ShouldBeFalse();
        });
    }

    [Fact]
    public async Task Verify_text_encoding_starts_git_without_github_tokens()
    {
        // Arrange
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        using var repository = new TemporaryGitRepository();
        await repository.Initialize(TestContext.Current.CancellationToken);
        repository.WriteText("valid.txt", "valid\n");
        await repository.Stage("valid.txt", TestContext.Current.CancellationToken);
        var environmentLogPath = Path.Combine(repository.RootPath, "git-environment.log");
        Dictionary<string, string?> environment = new(StringComparer.Ordinal)
        {
            ["PATH"] = repository.CreateGitShim(),
            ["TEXT_ENCODING_GIT_SHIM_ENVIRONMENT_LOG"] = environmentLogPath,
            ["GH_TOKEN"] = "credential-marker",
            ["GITHUB_TOKEN"] = "credential-marker"
        };

        // Act
        var result = await repository.RunCommand(
            ["verify-text-encoding", "--root", repository.RootPath],
            TestContext.Current.CancellationToken,
            environment: environment);
        var gitEnvironment = await File.ReadAllLinesAsync(environmentLogPath, TestContext.Current.CancellationToken);

        // Assert
        result.ExitCode.ShouldBe(0);
        gitEnvironment.ShouldContain("git-shim");
        gitEnvironment.ShouldNotContain("GH_TOKEN");
        gitEnvironment.ShouldNotContain("GITHUB_TOKEN");
    }

    [Fact]
    public async Task Verify_json_starts_git_from_path_without_github_tokens()
    {
        // Arrange
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        using var repository = new TemporaryGitRepository();
        await repository.Initialize(TestContext.Current.CancellationToken);
        repository.WriteText("valid.json", "{}");
        await repository.Stage("valid.json", TestContext.Current.CancellationToken);
        var environmentLogPath = Path.Combine(repository.RootPath, "git-environment.log");
        Dictionary<string, string?> environment = new(StringComparer.Ordinal)
        {
            ["PATH"] = repository.CreateGitShim(),
            ["TEXT_ENCODING_GIT_SHIM_ENVIRONMENT_LOG"] = environmentLogPath,
            ["GH_TOKEN"] = "credential-marker",
            ["GITHUB_TOKEN"] = "credential-marker"
        };

        // Act
        var result = await repository.RunCommand(
            ["verify-json", "--root", repository.RootPath],
            TestContext.Current.CancellationToken,
            environment: environment);
        var gitEnvironment = await File.ReadAllLinesAsync(environmentLogPath, TestContext.Current.CancellationToken);

        // Assert
        result.ExitCode.ShouldBe(0);
        gitEnvironment.ShouldContain(static line => line == "git-shim", "Git is resolved through PATH, where the shim comes first.");
        gitEnvironment.ShouldNotContain("GH_TOKEN");
        gitEnvironment.ShouldNotContain("GITHUB_TOKEN");
    }

    [Theory]
    [InlineData("verify-json")]
    [InlineData("dependency", "verify-license-exceptions")]
    public async Task Git_index_commands_ignore_repository_local_git_from_relative_path_entries(params string[] command)
    {
        // Arrange
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        using var repository = await LicenseExceptionTestRepository.Create(new LicenseScenario());
        var fakeGit = await RepositoryLocalGit.Create(repository.RootPath, TestContext.Current.CancellationToken);

        // Act
        var result = await repository.RunCommand(
            [.. command, "--root", repository.RootPath],
            TestContext.Current.CancellationToken,
            environment: fakeGit.EnvironmentVariables);

        // Assert
        result.ExitCode.ShouldBe(0, "The real Git lists valid JSON and a lock file that resolves the approved versions.");
        fakeGit.Ran.ShouldBeFalse("Git resolves only from absolute PATH entries, so the repository-local fake never runs.");
    }

    [Fact]
    public async Task Dependency_snapshot_ignores_repository_local_git_from_relative_path_entries()
    {
        // Arrange
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        using var repository = await DependencySnapshotTestRepository.Create(
            "{\"version\":2,\"dependencies\":{\"net10.0\":{\"A\":{\"type\":\"Direct\",\"resolved\":\"1.0.0\",\"contentHash\":\"HASH\"}}}}");
        var fakeGit = await RepositoryLocalGit.Create(repository.RootPath, TestContext.Current.CancellationToken);

        // The snapshot requires a clean checkout, so the reviewed commit carries the fake.
        await repository.Commit();

        // Act
        var result = await repository.Run(environment: fakeGit.EnvironmentVariables);

        // Assert
        result.ExitCode.ShouldBe(0, "The real Git reads the clean HEAD and the committed solution, project, and lock file.");
        fakeGit.Ran.ShouldBeFalse("Git resolves only from absolute PATH entries, so the committed fake never runs.");
    }

    [Fact]
    public async Task Automation_plan_ignores_repository_local_git_from_relative_path_entries()
    {
        // Arrange
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        using var repository = new TemporaryGitRepository();
        await repository.Initialize(TestContext.Current.CancellationToken);
        var commit = await repository.RunGit(
            ["-c", "user.name=Repo Config Tests", "-c", "user.email=repo-config-tests@example.invalid", "-c", "commit.gpgsign=false", "commit", "--allow-empty", "--quiet", "-m", "base"],
            TestContext.Current.CancellationToken);
        commit.ExitCode.ShouldBe(0, "Planning reads HEAD, so the repository needs a commit.");
        var fakeGit = await RepositoryLocalGit.Create(repository.RootPath, TestContext.Current.CancellationToken);
        Dictionary<string, string?> environment = new(fakeGit.EnvironmentVariables, StringComparer.Ordinal)
        {
            ["GITHUB_REPOSITORY"] = "owner/repository",
            ["GITHUB_RUN_ID"] = "42"
        };

        // Act
        var result = await repository.RunCommand(
            ["automation", "plan-github", "--root", repository.RootPath],
            TestContext.Current.CancellationToken,
            environment: environment);

        // Assert
        result.ExitCode.ShouldBe(1);
        result.StandardError.ShouldBe(
            "sharedkernel-repo: Roadmap automation detected a change outside roadmap/**." + Environment.NewLine,
            "The real Git reads HEAD and lists the untracked fake, and planning accepts changes only under roadmap/**.");
        fakeGit.Ran.ShouldBeFalse("Git resolves only from absolute PATH entries, so the repository-local fake never runs.");
    }
}
