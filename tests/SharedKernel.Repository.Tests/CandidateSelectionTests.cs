using PandaBear.CI.Tool;

namespace PandaBear.Repository.Tests;

public static class CandidateSelectionTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public static async Task Selection_appends_github_candidate_output_for_the_selected_repository(bool hasProjects)
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        using var repository = new RepositoryTestScope();
        var githubOutput = Path.Combine(repository.Root, "github-output.txt");
        var previousOutput = "existing=value" + Environment.NewLine;
        await File.WriteAllTextAsync(githubOutput, previousOutput, ct);
        if (!hasProjects)
        {
            await File.WriteAllTextAsync(Path.Combine(repository.Root, "SharedKernel.slnx"), "<Solution />", ct);
        }

        var processes = new ProcessLog();

        // Act
        var result = await RepositoryToolInvocation.Run(
            Path.GetDirectoryName(repository.Root)!,
            ["select", "--github-output", "github-output.txt", "--root", repository.Root],
            processes.Run,
            ct);

        // Assert
        result.ExitCode.ShouldBe(0);
        var expected = hasProjects ? "true" : "false";
        var appendedOutput = await File.ReadAllTextAsync(githubOutput, ct);
        appendedOutput.ShouldStartWith(previousOutput + "candidate_required=" + expected + Environment.NewLine, StringComparison.Ordinal);
        var candidateRequired = await File.ReadAllTextAsync(Path.Combine(repository.Root, "TestResults", "selection", "candidate-required.txt"), ct);
        candidateRequired.ShouldBe(expected);
    }

    [Fact]
    public static async Task Selection_without_git_history_requires_a_candidate_and_reports_the_missing_revision_on_standard_error()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        using var repository = new RepositoryTestScope();
        var processes = new ProcessLog(static (_, _, _) => throw new InvalidOperationException("Not a Git checkout."));

        // Act
        var result = await RepositoryToolInvocation.Run(repository.Root, ["select"], processes.Run, ct);

        // Assert
        result.ExitCode.ShouldBe(0);
        var selection = Path.Combine(repository.Root, "TestResults", "selection");
        var candidateRequired = await File.ReadAllTextAsync(Path.Combine(selection, "candidate-required.txt"), ct);
        candidateRequired.ShouldBe("true");
        var revision = await File.ReadAllTextAsync(Path.Combine(selection, "revision.txt"), ct);
        revision.ShouldBe("unavailable");
        result.Error.ShouldContain("Selection revision unavailable", StringComparison.Ordinal);
        result.Output.ShouldNotContain("Selection revision unavailable", StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("diff")]
    [InlineData("rev-parse")]
    public static async Task Selection_treats_a_git_failure_after_cancellation_as_the_cancellation(string gitCommand)
    {
        // Arrange
        using var repository = new RepositoryTestScope();
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var processes = new ProcessLog(async (_, arguments, _) =>
        {
            if (arguments[0] == gitCommand)
            {
                // Ctrl+C also stops Git, which can fail before the token is observed.
                await cancellation.CancelAsync();
                throw new InvalidOperationException("Command failed (130): git " + gitCommand);
            }

            return arguments[0] == "rev-parse" ? "fixture" : string.Empty;
        });

        // Act
        var result = await RepositoryToolInvocation.Run(repository.Root, ["select", "--base", "main"], processes.Run, cancellation.Token);

        // Assert
        result.ExitCode.ShouldBe(1);
        result.Output.ShouldBe(string.Empty);
        result.Error.ShouldBe(string.Empty);
        processes.Calls[^1].Arguments[0].ShouldBe(gitCommand);
        File.Exists(Path.Combine(repository.Root, "TestResults", "selection", "revision.txt")).ShouldBeFalse();
    }

    [Fact]
    public static async Task Selection_compares_the_base_revision_with_head_in_the_repository_root()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        using var repository = new RepositoryTestScope();
        var processes = new ProcessLog(static (_, arguments, _) => Task.FromResult(arguments[0] == "diff" ? "src/Core/Changed.cs\n" : "fixture"));

        // Act
        var result = await RepositoryToolInvocation.Run(repository.Root, ["select", "--base", "abc123"], processes.Run, ct);

        // Assert
        result.ExitCode.ShouldBe(0);
        var diff = processes.Calls[0];
        diff.FileName.ShouldBe("git");
        diff.Arguments.ShouldBe(["diff", "--name-only", "--no-renames", "abc123...HEAD", "--"]);
        diff.WorkingDirectory.ShouldBe(repository.Root);
        result.Output.ShouldContain("src/Core/Changed.cs: owning project src/Core/Core.csproj", StringComparison.Ordinal);
        result.Error.ShouldBe(string.Empty);
        var plan = await File.ReadAllTextAsync(Path.Combine(repository.Root, "TestResults", "selection", "selection.json"), ct);
        plan.ShouldContain("\"comparisonBase\": \"abc123\"", StringComparison.Ordinal);
    }

    [Fact]
    public static async Task Selection_writes_the_plan_to_the_selection_directory_under_the_root()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        using var repository = new RepositoryTestScope();
        var processes = new ProcessLog();

        // Act
        var result = await RepositoryToolInvocation.Run(
            Path.GetDirectoryName(repository.Root)!,
            ["select", "--root", repository.Root, "--selection", "plans/current"],
            processes.Run,
            ct);

        // Assert
        result.ExitCode.ShouldBe(0);
        var revision = await File.ReadAllTextAsync(Path.Combine(repository.Root, "plans", "current", "revision.txt"), ct);
        revision.ShouldBe("fixture");
        Directory.Exists(Path.Combine(repository.Root, "TestResults")).ShouldBeFalse();
    }

    [Theory]
    [InlineData("docs/release-policy.md", false)]
    [InlineData("README.md", false)]
    [InlineData("src/Core/README.md", true)]
    [InlineData("Directory.Build.props", true)]
    [InlineData("unknown-input", true)]
    public static void Package_inputs_require_candidates_while_repository_docs_do_not(string path, bool required)
    {
        // Arrange
        using var repository = new RepositoryTestScope();

        // Act
        var result = ProjectSelector.Select(repository.Root, [path]);

        // Assert
        var actualValue = result.BuildProjects.Length > 0;
        actualValue.ShouldBe(required);
    }

    [Fact]
    public static async Task Newly_added_hosted_tests_are_emitted_into_the_workflow_matrix()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        using var repository = new RepositoryTestScope();
        repository.AddProject("tests/NewHosted/NewHosted.csproj", """<Project Sdk="Aspire.AppHost.Sdk"><PropertyGroup><IsTestProject>true</IsTestProject></PropertyGroup></Project>""");
        var githubOutput = Path.Combine(repository.Root, "github-output.txt");
        var processes = new ProcessLog();

        // Act
        var result = await RepositoryToolInvocation.Run(repository.Root, ["select", "--github-output", githubOutput], processes.Run, ct);

        // Assert
        result.ExitCode.ShouldBe(0);
        var outputs = await File.ReadAllTextAsync(githubOutput, ct);
        outputs.ShouldContain("hosted_required=true", StringComparison.Ordinal);
        outputs.ShouldContain("NewHosted.csproj", StringComparison.Ordinal);
    }
}
