using System.Text.Json;
using SharedKernel.Processes;

namespace SharedKernel.CI.Tests;

/// <summary>Checks the forge-neutral workflow lint scope and failure evidence.</summary>
public sealed class WorkflowLintTests
{
    private const string ReportPath = "evidence/workflow-lint.json";
    private const string RemovedWorkflow = ".github/workflows/removed.yml";
    private const string NewWorkflow = ".github/workflows/changed name.yaml";
    private const string BaseWorkflow = ".github/workflows/base-only.yml";

    [Fact]
    public async Task Lint_uses_the_proposed_heads_merge_base_and_the_checked_out_existing_files()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        using var fixture = new TemporaryGitRepository();
        var root = fixture.RootPath;
        var runner = new SystemProcessRunner();
        var lintInvocations = new List<string[]>();
        var machine = new CiMachine(new ScriptedProcessRunner((start, output, token) =>
        {
            if (start.FileName == "actionlint")
            {
                lintInvocations.Add([.. start.ArgumentList]);
                return Task.FromResult(new ProcessOutput(17, "synthetic lint finding"));
            }

            return runner.Run(start, output, token);
        }), new ExecutableMap(static executable => executable == "git" ? PathExecutableResolver.Default.Find(executable)! : executable), TimeProvider.System);
        await fixture.Initialize(ct);
        await fixture.Commit(RemovedWorkflow, "removed", "Initial workflow", ct);
        var fork = (await fixture.RunGit(["rev-parse", "HEAD"], ct)).Trim();
        await fixture.Commit(BaseWorkflow, "base", "Advance base", ct);
        var baseRevision = (await fixture.RunGit(["rev-parse", "HEAD"], ct)).Trim();
        await fixture.RunGit(["checkout", "--quiet", "--detach", fork], ct);
        await fixture.RunGit(["rm", "--", RemovedWorkflow], ct);
        await fixture.Commit(NewWorkflow, "head", "Update workflow", ct);
        var headRevision = (await fixture.RunGit(["rev-parse", "HEAD"], ct)).Trim();
        await fixture.RunGit(["merge", "--quiet", "--no-edit", baseRevision], ct);
        var linter = new WorkflowLint(machine);

        // Act
        Func<Task> lint = () => linter.Run(root, baseRevision, headRevision, "actionlint", ReportPath, TextWriter.Null, ct);
        var failure = await lint.ShouldThrow<InvalidOperationException>();

        // Assert
        failure.Message.ShouldContain("Command failed (17)", StringComparison.Ordinal);
        lintInvocations.ShouldHaveSingleItem().ShouldBe(["-color", "--", NewWorkflow]);
        using var report = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(root, ReportPath), ct));
        report.RootElement.GetProperty("mode").GetString().ShouldBe("changed");
        report.RootElement.GetProperty("outcome").GetString().ShouldBe("failed");
        report.RootElement.GetProperty("paths")[0].GetString().ShouldBe(NewWorkflow);
    }

    [Theory]
    [InlineData(".github/actions/example/action.yml\0", "false\n\n", "shared-lint-input")]
    [InlineData(".forgejo/actions/example/action.yml\0", "false\n\n", "shared-lint-input")]
    [InlineData(".github/actionlint.yml\0", "false\n\n", "shared-lint-input")]
    [InlineData(".github/actionlint.yaml\0", "false\n\n", "shared-lint-input")]
    [InlineData("", "true\n\n", "unverified-checkout")]
    [InlineData("", "false\nsubdir/\n", "unverified-checkout")]
    [InlineData("", "false\n\n", "ambiguous-merge-base")]
    public async Task Uncertain_scope_or_shared_inputs_use_a_full_pass(string changes, string checkout, string reason)
    {
        // Arrange
        using var fixture = new RepositoryTestScope();
        var baseRevision = new string('a', 40);
        var headRevision = new string('b', 40);
        var processes = new ProcessLog((_, arguments, _) => Task.FromResult(arguments switch
        {
            ["rev-parse", ..] => checkout,
            ["merge-base", "--all", ..] => reason == "ambiguous-merge-base" ? $"{baseRevision}\n{headRevision}" : baseRevision,
            ["--literal-pathspecs", "diff", ..] => changes,
            _ => string.Empty,
        }));

        // Act
        await new WorkflowLint(processes.Machine()).Run(fixture.Root, baseRevision, headRevision, "actionlint", ReportPath,
            TextWriter.Null, TestContext.Current.CancellationToken);

        // Assert
        processes.Commands[^1].ShouldBe("actionlint -color");
        using var report = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(fixture.Root, ReportPath), TestContext.Current.CancellationToken));
        report.RootElement.GetProperty("mode").GetString().ShouldBe("full");
        report.RootElement.GetProperty("reason").GetString().ShouldBe(reason);
        report.RootElement.GetProperty("outcome").GetString().ShouldBe("passed");
    }

    [Fact]
    public async Task A_deletion_only_comparison_does_not_start_the_linter()
    {
        // Arrange
        using var fixture = new RepositoryTestScope();
        var baseRevision = new string('a', 40);
        var headRevision = new string('b', 40);
        var processes = new ProcessLog((_, arguments, _) => Task.FromResult(arguments switch
        {
            ["rev-parse", ..] => "false\n\n",
            ["merge-base", "--all", ..] => baseRevision,
            ["--literal-pathspecs", "diff", ..] => RemovedWorkflow + '\0',
            _ => string.Empty,
        }));

        // Act
        await new WorkflowLint(processes.Machine()).Run(fixture.Root, baseRevision, headRevision, "actionlint", ReportPath,
            TextWriter.Null, TestContext.Current.CancellationToken);

        // Assert
        processes.Calls.ShouldAllSatisfy(static call => call.FileName.ShouldBe("git"));
        using var report = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(fixture.Root, ReportPath), TestContext.Current.CancellationToken));
        report.RootElement.GetProperty("mode").GetString().ShouldBe("none");
    }

    [Fact]
    public async Task A_custom_workflow_directory_is_linted_explicitly_in_full_mode()
    {
        // Arrange
        using var fixture = new RepositoryTestScope();
        fixture.WriteFile(".forgejo/workflows/review.yaml", "custom");
        fixture.WriteFile(".forgejo/workflows/nested/changed name.yml", "custom");
        fixture.WriteFile(".forgejo/workflows/README.md", "not a workflow");
        fixture.WriteFile(BaseWorkflow, "default path");
        var processes = new ProcessLog();

        // Act
        await new WorkflowLint(processes.Machine(), ".forgejo/workflows").Run(fixture.Root, null, null, "actionlint", ReportPath,
            TextWriter.Null, TestContext.Current.CancellationToken);

        // Assert
        processes.Calls.ShouldHaveSingleItem().Arguments.ShouldBe(["-color", "--", ".forgejo/workflows/nested/changed name.yml", ".forgejo/workflows/review.yaml"]);
        using var report = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(fixture.Root, ReportPath), TestContext.Current.CancellationToken));
        report.RootElement.GetProperty("mode").GetString().ShouldBe("full");
        report.RootElement.GetProperty("paths").GetArrayLength().ShouldBe(2);
    }
}
