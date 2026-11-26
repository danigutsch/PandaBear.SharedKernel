using SharedKernel.Repository.Tests.Conventions;

namespace SharedKernel.Repository.Tests;

/// <summary>Checks the hosted selection and required aggregate across CI event groups.</summary>
public static class CiTaskGroupWorkflowTests
{
    [Fact]
    public static void Only_pull_requests_pass_a_base_revision_to_source_selection()
    {
        // Arrange
        var root = PackagingConventionTestFiles.GetRepositoryRoot();
        var workflow = File.ReadAllText(Path.Combine(root, ".github", "workflows", "ci.yml"));

        // Act
        var selection = WorkflowJobs.Read(workflow, "selection");

        // Assert
        selection.ShouldContain(
            "BASE_SHA: ${{ github.event_name == 'pull_request' && github.event.pull_request.base.sha || '' }}",
            StringComparison.Ordinal,
            "Main, scheduled and ordinary manual runs select the whole solution.");
        workflow.ShouldContain("  push:\n    branches: [main]\n", StringComparison.Ordinal);
        workflow.ShouldContain("  schedule:\n", StringComparison.Ordinal);
        selection.ShouldContain("inputs.candidate_version == '' && inputs.baseline_candidate_version == ''", StringComparison.Ordinal);
    }

    [Fact]
    public static void The_required_aggregate_still_runs_when_a_dependency_fails_or_is_skipped()
    {
        // Arrange
        var root = PackagingConventionTestFiles.GetRepositoryRoot();
        var workflow = File.ReadAllText(Path.Combine(root, ".github", "workflows", "ci.yml"));

        // Act
        var triggers = workflow[..workflow.IndexOf("\npermissions:\n", StringComparison.Ordinal)];
        var aggregate = WorkflowJobs.Read(workflow, "validation-summary");

        // Assert
        triggers.ShouldNotContain("paths:", StringComparison.Ordinal);
        triggers.ShouldNotContain("paths-ignore:", StringComparison.Ordinal);
        aggregate.ShouldContain("'SharedKernel Validation'", StringComparison.Ordinal);
        aggregate.ShouldContain("always()", StringComparison.Ordinal);
        aggregate.ShouldContain("test \"$SELECTION_RESULT\" = success", StringComparison.Ordinal);
        aggregate.ShouldContain("test \"$QUALITY_RESULT\" = success", StringComparison.Ordinal);
    }
}
