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
}
