using PandaBear.Repository.Tests.Conventions;

namespace PandaBear.Repository.Tests;

public sealed class WorkflowContractTests
{
    [Fact]
    public void Documentation_only_validation_avoids_bootstrap_tests_and_full_solution_formatting()
    {
        // Arrange
        var root = SharedKernelPackagingConventionTestFiles.GetRepositoryRoot();
        var workflow = File.ReadAllText(Path.Combine(root, ".github", "workflows", "ci.yml"));

        // Act
        var quality = workflow.Split("  repository-quality:", StringSplitOptions.None)[1]
            .Split("\n  validation-summary:", StringSplitOptions.None)[0];

        // Assert
        workflow.ShouldContain("github.event_name == 'push' && github.event.before", StringComparison.Ordinal);
        quality.ShouldContain("needs: selection", StringComparison.Ordinal);
        quality.ShouldContain("if: needs.selection.outputs.candidate_required == 'true'\n        run: bash setup-dev.sh", StringComparison.Ordinal);
        quality.ShouldContain("if: needs.selection.outputs.candidate_required == 'true'\n        run: |\n          dotnet test", StringComparison.Ordinal);
        quality.ShouldContain("if: needs.selection.outputs.candidate_required == 'true'\n        run: dotnet format", StringComparison.Ordinal);
        quality.ShouldContain("if: needs.selection.outputs.candidate_required == 'false'\n        uses: ./.github/actions/setup-dotnet-tool", StringComparison.Ordinal);
        quality.ShouldContain("project: tools/SharedKernel.Documentation.Tool", StringComparison.Ordinal);
        quality.ShouldContain("-- verify --config docs/architecture/generated-diagrams.json\n", StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("tools/SharedKernel.Repository.Tool", "select")]
    [InlineData("tools/SharedKernel.Repository.Tool", "validate")]
    [InlineData("tools/SharedKernel.Versioning.Tool", "calculate-release")]
    [InlineData("tools/SharedKernel.Versioning.Tool", "create-candidate")]
    [InlineData("tools/SharedKernel.Versioning.Tool", "consume-candidate")]
    [InlineData("tools/SharedKernel.Documentation.Tool", "verify")]
    public void Active_ci_delegates_repository_operations_to_existing_dotnet_tools(string project, string command)
    {
        // Arrange
        var root = SharedKernelPackagingConventionTestFiles.GetRepositoryRoot();
        var workflow = File.ReadAllText(Path.Combine(root, ".github", "workflows", "ci.yml"))
            .Replace("\\\n", string.Empty, StringComparison.Ordinal);
        var commands = workflow.Split('\n').Select(static line => string.Join(' ', line.Trim()
            .Replace("run: ", string.Empty, StringComparison.Ordinal).Split(' ', StringSplitOptions.RemoveEmptyEntries)));

        // Assert
        commands.ShouldContain(line => line.StartsWith("dotnet run --project " + project + " ", StringComparison.Ordinal)
            && line.Contains("-- " + command, StringComparison.Ordinal));
    }

    [Fact]
    public void Published_candidate_dispatch_consumes_exact_artifacts_on_both_platforms_without_repacking()
    {
        // Arrange
        var root = SharedKernelPackagingConventionTestFiles.GetRepositoryRoot();
        var workflow = File.ReadAllText(Path.Combine(root, ".github", "workflows", "ci.yml"));
        var consumption = workflow.Split("  published-candidate-consumption:", StringSplitOptions.None)[1]
            .Split("\n  validation-summary:", StringSplitOptions.None)[0];

        // Assert
        workflow.ShouldContain("&& inputs.candidate_version == ''", StringComparison.Ordinal);
        consumption.ShouldContain("os: [ubuntu-latest, windows-latest]", StringComparison.Ordinal);
        consumption.ShouldContain("CANDIDATE_VERSION: ${{ inputs.candidate_version }}", StringComparison.Ordinal);
        consumption.ShouldContain("--repo $env:GITHUB_REPOSITORY", StringComparison.Ordinal);
        consumption.ShouldContain("$candidate.packageVersion -cne $env:CANDIDATE_VERSION", StringComparison.Ordinal);
        consumption.ShouldContain("Compare-Object $expectedFiles $actualFiles", StringComparison.Ordinal);
        consumption.ShouldContain("ref: ${{ steps.candidate.outputs.library_revision }}", StringComparison.Ordinal);
        consumption.ShouldContain("$revision -cne $env:CANDIDATE_REVISION -or $dirty", StringComparison.Ordinal);
        consumption.ShouldContain("-- consume-candidate --candidate $manifest --output $evidence --aot", StringComparison.Ordinal);
        consumption.ShouldNotContain("-- create-candidate", StringComparison.Ordinal);
        consumption.ShouldContain("if: always()", StringComparison.Ordinal);
        consumption.ShouldContain("published-candidate/consumption.json", StringComparison.Ordinal);
        workflow.ShouldContain("test \"$PUBLISHED_CANDIDATE_RESULT\" = success", StringComparison.Ordinal);
    }

    [Fact]
    public void Active_workflows_do_not_execute_archived_extraction_scripts()
    {
        // Arrange
        var root = SharedKernelPackagingConventionTestFiles.GetRepositoryRoot();
        var workflows = Directory.GetFiles(Path.Combine(root, ".github", "workflows"), "*.y*ml");

        // Assert
        workflows.ShouldNotBeEmpty();
        foreach (var workflow in workflows)
        {
            File.ReadAllText(workflow).ShouldNotContain("provenance/extraction-tools/", StringComparison.Ordinal);
        }
    }
}
