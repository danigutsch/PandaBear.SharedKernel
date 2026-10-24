using System.CommandLine;
using PandaBear.CI.Tool;
using PandaBear.Repository.Tests.Conventions;

namespace PandaBear.Repository.Tests;

public sealed class RepositoryToolCallerTests
{
    [Fact]
    public void Every_repository_tool_command_line_in_workflows_and_the_tool_readme_parses()
    {
        // Arrange
        var root = SharedKernelPackagingConventionTestFiles.GetRepositoryRoot();
        var command = RepositoryCommandLine.Create(root, static (_, _, _, _) => Task.FromResult(string.Empty));

        // Act
        var failures = RepositoryToolCallers.Find(root)
            .Select(caller => (Caller: caller, Result: command.Parse(caller.Arguments)))
            .Where(static parsed => parsed.Result.Errors.Count > 0 || parsed.Result.CommandResult.Command is RootCommand)
            .Select(static parsed => parsed.Caller.File + ": " + string.Join(' ', parsed.Caller.Arguments) + " => "
                + string.Join("; ", parsed.Result.Errors.Select(static error => error.Message)))
            .ToArray();

        // Assert
        failures.ShouldBeEmpty();
    }

    [Fact]
    public void Ci_selection_passes_the_base_revision_only_when_one_exists()
    {
        // Arrange
        var root = SharedKernelPackagingConventionTestFiles.GetRepositoryRoot();

        // Act
        var selections = RepositoryToolCallers.Find(root)
            .Where(static caller => caller.File == ".github/workflows/ci.yml" && caller.Arguments is ["select", ..])
            .Select(static caller => string.Join(' ', caller.Arguments))
            .ToArray();

        // Assert
        selections.ShouldBe(["select --github-output $GITHUB_OUTPUT", "select --github-output $GITHUB_OUTPUT --base $BASE_SHA"]);
    }

    [Theory]
    [InlineData(".github/workflows/ci.yml", "select")]
    [InlineData(".github/workflows/ci.yml", "validate")]
    [InlineData(".github/workflows/pr-metadata.yml", "verify-pr-title")]
    [InlineData("tools/SharedKernel.CI.Tool/README.md", "select")]
    [InlineData("tools/SharedKernel.CI.Tool/README.md", "validate")]
    [InlineData("tools/SharedKernel.CI.Tool/README.md", "verify-pr-title")]
    public void Callers_invoke_the_repository_tool_command(string file, string commandName)
    {
        // Arrange
        var root = SharedKernelPackagingConventionTestFiles.GetRepositoryRoot();
        var command = RepositoryCommandLine.Create(root, static (_, _, _, _) => Task.FromResult(string.Empty));

        // Act
        var commandNames = RepositoryToolCallers.Find(root)
            .Where(caller => caller.File == file)
            .Select(caller => command.Parse(caller.Arguments).CommandResult.Command.Name)
            .ToArray();

        // Assert
        commandNames.ShouldContain(commandName);
    }
}
