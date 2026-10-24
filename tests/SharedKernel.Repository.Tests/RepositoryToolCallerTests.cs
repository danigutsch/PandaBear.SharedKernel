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
    public void Ci_selection_passes_the_base_revision_on_one_command_line()
    {
        // Arrange
        var root = SharedKernelPackagingConventionTestFiles.GetRepositoryRoot();

        // Act
        var selections = RepositoryToolCallers.Find(root)
            .Where(static caller => caller.File == ".github/workflows/ci.yml" && caller.Arguments is ["select", ..])
            .Select(static caller => string.Join(' ', caller.Arguments))
            .ToArray();

        // Assert
        selections.ShouldBe(["select --base $BASE_SHA --github-output $GITHUB_OUTPUT"], "An empty base selects every test project with a reason.");
    }

    [Fact]
    public void Tool_readme_runs_the_installed_tool_and_the_project()
    {
        // Arrange
        var root = SharedKernelPackagingConventionTestFiles.GetRepositoryRoot();

        // Act
        var readmeCallers = RepositoryToolCallers.Find(root)
            .Where(static caller => caller.File == "tools/SharedKernel.CI.Tool/README.md")
            .Select(static caller => string.Join(' ', caller.Arguments))
            .ToArray();

        // Assert
        readmeCallers.ShouldBe(
            [
                "verify-commit-message --file <commit-message-file>",
                "select --base <object-id>",
                "run --slice library",
                "openapi refresh --target library",
                "install-playwright --project <test-project> --browser chromium --with-dependencies",
                "stage-candidate --pin <candidate.json> --library-root . --output <feed> --repository danigutsch/PandaBear.SharedKernel",
                "verify-commit-message --event <event.json>",
                "run --slice library --coverage --install-playwright --playwright-dependencies --github-summary $GITHUB_STEP_SUMMARY",
                "stage-candidate --pin .config/sharedkernel-candidate.json --library-root <sharedkernel-checkout> --output .local/sharedkernel-feed"
                    + " --repository danigutsch/PandaBear.SharedKernel",
                "stage-candidate --pin .config/sharedkernel-candidate.json --library-root <sharedkernel-checkout> --output .local/sharedkernel-feed"
                    + " --feed <packages>",
                "verify-commit-message --file $1",
            ],
            "Installation and the commit-msg hook run the installed sharedkernel-ci, and this repository runs the project.");
    }

    [Theory]
    [InlineData(".github/workflows/ci.yml", "select")]
    [InlineData(".github/workflows/ci.yml", "run")]
    [InlineData(".github/workflows/ci.yml", "stage-candidate")]
    [InlineData(".github/workflows/pr-metadata.yml", "verify-commit-message")]
    [InlineData("tools/SharedKernel.CI.Tool/README.md", "select")]
    [InlineData("tools/SharedKernel.CI.Tool/README.md", "run")]
    [InlineData("tools/SharedKernel.CI.Tool/README.md", "refresh")]
    [InlineData("tools/SharedKernel.CI.Tool/README.md", "install-playwright")]
    [InlineData("tools/SharedKernel.CI.Tool/README.md", "stage-candidate")]
    [InlineData("tools/SharedKernel.CI.Tool/README.md", "verify-commit-message")]
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
