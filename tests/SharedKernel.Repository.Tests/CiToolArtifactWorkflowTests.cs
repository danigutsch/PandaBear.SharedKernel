using SharedKernel.Repository.Tests.Conventions;

namespace SharedKernel.Repository.Tests;

/// <summary>Guards the selection job's tool artifact and the sparse tool restore cache.</summary>
public sealed class CiToolArtifactWorkflowTests
{
    [Fact]
    public void Validation_and_coverage_use_the_tool_artifact_from_selection()
    {
        // Arrange
        var root = PackagingConventionTestFiles.GetRepositoryRoot();
        var workflow = File.ReadAllText(Path.Combine(root, ".github", "workflows", "ci.yml")).Replace("\r\n", "\n", StringComparison.Ordinal);

        // Act
        var selection = WorkflowJobs.Read(workflow, "selection");
        var validate = WorkflowJobs.Read(workflow, "validate");
        var coverage = WorkflowJobs.Read(workflow, "coverage-gate");

        // Assert
        selection.ShouldContain("ci_tool_artifact_id: ${{ steps.ci-tool-artifact.outputs.artifact-id }}", StringComparison.Ordinal);
        selection.ShouldContain("if: steps.selection.outputs.build_required == 'true' && steps.selection.outputs.matrix != '[]'", StringComparison.Ordinal);
        selection.ShouldContain("path: ${{ runner.temp }}/sharedkernel-ci-tool", StringComparison.Ordinal);
        foreach (var consumer in new[] { validate, coverage })
        {
            consumer.ShouldContain("uses: ./.github/actions/download-selection-tool", StringComparison.Ordinal);
            consumer.ShouldContain("artifact-id: ${{ needs.selection.outputs.ci_tool_artifact_id }}", StringComparison.Ordinal);
            consumer.ShouldNotContain("project: tools/SharedKernel.CI.Tool", StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Selection_tool_download_checks_the_checkout_and_payload_before_use()
    {
        // Arrange
        var root = PackagingConventionTestFiles.GetRepositoryRoot();
        var path = Path.Combine(root, ".github", "actions", "download-selection-tool", "action.yml");

        // Act
        var action = File.ReadAllText(path);

        // Assert
        action.ShouldContain("artifact-ids: ${{ inputs.artifact-id }}", StringComparison.Ordinal);
        action.ShouldNotContain("github-token:", StringComparison.Ordinal);
        action.ShouldNotContain("run-id:", StringComparison.Ordinal);
        action.ShouldContain("git rev-parse HEAD", StringComparison.Ordinal);
        action.ShouldContain("sha256sum --check --strict checksums.sha256", StringComparison.Ordinal);
    }

    [Fact]
    public void The_sparse_source_build_caches_the_selected_tools_locked_restore()
    {
        // Arrange
        var root = PackagingConventionTestFiles.GetRepositoryRoot();
        var workflow = File.ReadAllText(Path.Combine(root, ".github", "workflows", "ci.yml")).Replace("\r\n", "\n", StringComparison.Ordinal);

        // Act
        var source = WorkflowJobs.Read(workflow, "source-build");

        // Assert
        source.ShouldContain("cache: true\n          cache-dependency-path: source-build/tools/${{ matrix.tool }}/packages.lock.json", StringComparison.Ordinal);
        source.ShouldContain("run: dotnet build \"tools/$TOOL\" -c Release", StringComparison.Ordinal);
        source.ShouldNotContain("download-selection-tool", StringComparison.Ordinal);
    }
}
