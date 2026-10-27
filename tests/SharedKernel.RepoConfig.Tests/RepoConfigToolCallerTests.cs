namespace PandaBear.RepoConfig.Tests;

/// <summary>Checks that the repo-config command lines used in the repository's workflow files match the tool's command tree.</summary>
[Trait(TestTraitNames.CategoryName, TestTraits.CommandLineCategory)]
public sealed class RepoConfigToolCallerTests
{
    [Fact]
    public void Every_workflow_command_line_parses_to_a_command()
    {
        // Arrange
        var root = CiTestSelectionTestProcess.GetRepositoryRoot();
        var rootCommand = RepoConfigCommandLine.Create(new RepoConfigToolEnvironment(root));

        // Act
        var callers = RepoConfigToolCallers.Find(root).ToArray();
        var failures = callers
            .Select(caller => (Caller: caller, Result: rootCommand.Parse(caller.Arguments)))
            .Where(static parsed => parsed.Result.Errors.Count > 0
                || parsed.Result.Action is null
                || !ReferenceEquals(parsed.Result.Action, parsed.Result.CommandResult.Command.Action))
            .Select(static parsed => parsed.Caller.File + ": " + string.Join(' ', parsed.Caller.Arguments) + " => "
                + string.Join("; ", parsed.Result.Errors.Select(static error => error.Message)))
            .ToArray();

        // Assert
        callers.ShouldNotBeEmpty();
        failures.ShouldBeEmpty();
    }

    [Theory]
    [InlineData(".github/workflows/security.yml", "baseline")]
    [InlineData(".github/workflows/security.yml", "scan")]
    public void Workflows_invoke_the_repo_config_tool_command(string file, string commandName)
    {
        // Arrange
        var root = CiTestSelectionTestProcess.GetRepositoryRoot();
        var rootCommand = RepoConfigCommandLine.Create(new RepoConfigToolEnvironment(root));

        // Act
        var commandNames = RepoConfigToolCallers.Find(root)
            .Where(caller => caller.File == file)
            .Select(caller => rootCommand.Parse(caller.Arguments).CommandResult.Command.Name)
            .ToArray();

        // Assert
        commandNames.ShouldContain(commandName);
    }

    [Fact]
    public async Task Workflow_scan_reads_source_built_and_installed_tool_command_lines()
    {
        // Arrange
        using var root = new TemporaryDirectory();
        var workflows = Path.Combine(root.Path, ".github", "workflows");
        Directory.CreateDirectory(workflows);
        await File.WriteAllTextAsync(
            Path.Combine(workflows, "example.yml"),
            """
            jobs:
              check:
                steps:
                  - uses: ./.github/actions/setup-dotnet-tool
                    with:
                      project: tools/SharedKernel.RepoConfig.Tool
                  - run: dotnet run --project tools/SharedKernel.RepoConfig.Tool -c Release --no-build -- verify-json --root .
                  - run: |
                      dotnet tool run sharedkernel-repo -- dependency verify-license-exceptions \
                        --github-output "$GITHUB_OUTPUT"
                  - run: sharedkernel-repo verify --root .
                  - run: echo "sharedkernel-repo: is an error prefix" && dotnet test --project tests/SharedKernel.RepoConfig.Tests
            """,
            TestContext.Current.CancellationToken);

        // Act
        var arguments = RepoConfigToolCallers.Find(root.Path)
            .Select(static caller => string.Join(' ', caller.Arguments))
            .ToArray();

        // Assert
        arguments.ShouldBe(
            [
                "verify-json --root .",
                "dependency verify-license-exceptions --github-output $GITHUB_OUTPUT",
                "verify --root .",
            ],
            "The action input names the project without arguments, and an error prefix is not a command line.");
    }
}
