using SharedKernel.Repository.Tests.Conventions;

namespace SharedKernel.Repository.Tests;

/// <summary>Guards the Actionlint adapter and the full-validation events.</summary>
public sealed class ActionlintWorkflowTests
{
    [Fact]
    public void Only_the_opted_in_pull_request_uses_the_portable_lint_tool()
    {
        // Arrange
        var workflows = Path.Combine(PackagingConventionTestFiles.GetRepositoryRoot(), ".github", "workflows");
        var caller = File.ReadAllText(Path.Combine(workflows, "actionlint.yml"));
        var reusable = File.ReadAllText(Path.Combine(workflows, "actionlint-reusable.yml"));

        // Act
        var call = WorkflowJobs.Read(caller, "actionlint");
        var job = WorkflowJobs.Read(reusable, "actionlint");
        var input = reusable.Split("      changed-workflows-on-pull-request:\n", StringSplitOptions.None)[1];

        // Assert
        caller.ShouldContain("  push:\n    branches: [main]\n  schedule:\n", StringComparison.Ordinal);
        caller.ShouldContain("  workflow_dispatch:\n", StringComparison.Ordinal);
        call.ShouldContain("      changed-workflows-on-pull-request: true\n", StringComparison.Ordinal);
        input.ShouldContain("type: boolean\n        default: false\n", StringComparison.Ordinal);
        job.ShouldContain("inputs.changed-workflows-on-pull-request && github.event_name == 'pull_request' && '0' || '1'", StringComparison.Ordinal);
        job.ShouldContain(
            "if: inputs.changed-workflows-on-pull-request && github.event_name == 'pull_request'\n"
                + "        uses: ./.github/actions/setup-dotnet-tool",
            StringComparison.Ordinal);
        job.ShouldContain(
            "          BASE_SHA: ${{ github.event.pull_request.base.sha }}\n"
                + "          HEAD_SHA: ${{ github.event.pull_request.head.sha }}",
            StringComparison.Ordinal);
        job.ShouldContain("-- verify-workflows --base \"$BASE_SHA\" --head \"$HEAD_SHA\"", StringComparison.Ordinal);
        job.ShouldContain(
            "if: ${{ !inputs.changed-workflows-on-pull-request || github.event_name != 'pull_request' }}\n"
                + "        run: actionlint -color",
            StringComparison.Ordinal);
    }
}
