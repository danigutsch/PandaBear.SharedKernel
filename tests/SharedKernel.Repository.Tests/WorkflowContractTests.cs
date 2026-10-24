using System.Xml.Linq;
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

    [Fact]
    public void Source_build_builds_each_source_runnable_tool_from_a_sparse_checkout_and_runs_it_from_outside()
    {
        // Arrange
        var root = SharedKernelPackagingConventionTestFiles.GetRepositoryRoot();
        var workflow = File.ReadAllText(Path.Combine(root, ".github", "workflows", "ci.yml"));
        var buildProperties = XDocument.Load(Path.Combine(root, "Directory.Build.props"));

        // Act
        var job = WorkflowJobs.Read(workflow, "source-build");
        var checkouts = workflow.Split('\n')
            .Select(static line => line.Trim())
            .Where(static line => line.StartsWith("- uses: actions/checkout@", StringComparison.Ordinal))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var lockedRestoreConditions = buildProperties.Descendants("PropertyGroup")
            .Where(static group => group.Elements("RestoreLockedMode").Any(static mode => mode.Value == "true"))
            .Select(static group => (string?)group.Attribute("Condition") ?? string.Empty)
            .ToArray();

        // Assert
        var checkout = checkouts.ShouldHaveSingleItem();
        job.ShouldContain("    needs: selection\n    if: needs.selection.outputs.candidate_required == 'true'\n", StringComparison.Ordinal);
        job.ShouldContain("        os: [ubuntu-latest, windows-latest]\n        tool: [SharedKernel.CI.Tool, SharedKernel.RepoConfig.Tool]\n", StringComparison.Ordinal);
        job.ShouldContain(
            "      " + checkout + "\n        with:\n          path: source-build\n          persist-credentials: false\n"
                + "          sparse-checkout: |\n            tools/${{ matrix.tool }}\n            tools/Shared\n          sparse-checkout-cone-mode: true\n",
            StringComparison.Ordinal);
        job.ShouldContain("          global-json-file: source-build/global.json\n", StringComparison.Ordinal);
        job.ShouldContain(
            "        working-directory: source-build\n        shell: bash\n        env:\n          TOOL: ${{ matrix.tool }}\n"
                + "        run: dotnet build \"tools/$TOOL\" -c Release\n",
            StringComparison.Ordinal);
        job.ShouldContain(
            "      - name: Run the tool from outside its checkout\n        shell: bash\n        env:\n          TOOL: ${{ matrix.tool }}\n"
                + "        run: dotnet run --project \"source-build/tools/$TOOL\" -c Release --no-build -- --help\n",
            StringComparison.Ordinal);
        lockedRestoreConditions.ShouldContain(
            static condition => condition.Contains("'$(GITHUB_ACTIONS)' == 'true'", StringComparison.Ordinal),
            "The build step restores in locked mode because GitHub Actions sets GITHUB_ACTIONS.");
    }

    [Fact]
    public void Validation_summary_requires_the_source_build_whenever_a_candidate_is_required()
    {
        // Arrange
        var root = SharedKernelPackagingConventionTestFiles.GetRepositoryRoot();
        var workflow = File.ReadAllText(Path.Combine(root, ".github", "workflows", "ci.yml"));

        // Act
        var summary = WorkflowJobs.Read(workflow, "validation-summary");
        var needs = summary.Split('\n')
            .Select(static line => line.Trim())
            .ShouldHaveSingleItem(static line => line.StartsWith("needs: [", StringComparison.Ordinal))["needs: [".Length..^1]
            .Split(", ");

        // Assert
        needs.ShouldContain("source-build");
        summary.ShouldContain("          SOURCE_BUILD_RESULT: ${{ needs.source-build.result }}\n", StringComparison.Ordinal);
        summary.ShouldContain(
            "if [[ \"$CANDIDATE_REQUIRED\" == true ]]; then\n"
                + "            for result in \"$LIBRARY_RESULT\" \"$SOURCE_BUILD_RESULT\" \"$CANDIDATE_RESULT\" \"$LINUX_RESULT\" \"$WINDOWS_RESULT\"; do\n",
            StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("tools/SharedKernel.CI.Tool", "select")]
    [InlineData("tools/SharedKernel.CI.Tool", "validate")]
    [InlineData("tools/SharedKernel.CI.Tool", "stage-candidate")]
    [InlineData("tools/SharedKernel.Versioning.Tool", "calculate-release")]
    [InlineData("tools/SharedKernel.Versioning.Tool", "create-candidate")]
    [InlineData("tools/SharedKernel.Versioning.Tool", "consume-candidate")]
    [InlineData("tools/SharedKernel.Documentation.Tool", "verify")]
    public void Active_ci_delegates_repository_operations_to_existing_dotnet_tools(string project, string command)
    {
        // Arrange
        var root = SharedKernelPackagingConventionTestFiles.GetRepositoryRoot();

        // Act
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

        // Act
        var consumption = WorkflowJobs.Read(workflow, "published-candidate-consumption");
        var steps = consumption.Split("\n      - ", StringSplitOptions.None);
        var discovery = steps.ShouldHaveSingleItem(static step => step.Contains("gh release view", StringComparison.Ordinal));

        // Assert
        workflow.ShouldContain("&& inputs.candidate_version == ''", StringComparison.Ordinal);
        consumption.ShouldContain("os: [ubuntu-latest, windows-latest]", StringComparison.Ordinal);
        discovery.ShouldContain("CANDIDATE_VERSION: ${{ inputs.candidate_version }}", StringComparison.Ordinal);
        discovery.ShouldContain(
            "if ($env:CANDIDATE_VERSION -cnotmatch '^[0-9]+\\.[0-9]+\\.[0-9]+-[0-9A-Za-z]+(?:[.-][0-9A-Za-z]+)*$')", StringComparison.Ordinal);
        discovery.ShouldContain("--repo $env:GITHUB_REPOSITORY --json isPrerelease,assets\n", StringComparison.Ordinal);
        discovery.ShouldContain("if ($release.isPrerelease -ne $true)", StringComparison.Ordinal);
        discovery.ShouldContain("--dir $directory --pattern candidate.json\n", StringComparison.Ordinal);
        discovery.Split("--pattern").Length.ShouldBe(2, "Only candidate.json is downloaded; stage-candidate downloads and verifies the packages.");
        discovery.ShouldContain("$candidate.packageVersion -cne $env:CANDIDATE_VERSION", StringComparison.Ordinal);
        discovery.ShouldContain("Compare-Object $expectedAssets $actualAssets -CaseSensitive", StringComparison.Ordinal);
        consumption.Split("GH_TOKEN").Length.ShouldBe(2, "Only the gh step receives a token; staging downloads the public release without one.");
        consumption.ShouldContain("ref: ${{ steps.candidate.outputs.library_revision }}", StringComparison.Ordinal);
        consumption.ShouldContain("project: tools/SharedKernel.CI.Tool\n", StringComparison.Ordinal);
        consumption.ShouldContain("project: tools/SharedKernel.Versioning.Tool\n", StringComparison.Ordinal);
        consumption.ShouldContain(
            "-- stage-candidate --pin $manifest --library-root . --output $feed --repository $env:GITHUB_REPOSITORY\n", StringComparison.Ordinal);
        consumption.ShouldContain("-- consume-candidate --candidate $staged --output $evidence --aot\n", StringComparison.Ordinal);
        consumption.ShouldNotContain("Verify exact clean source revision", StringComparison.Ordinal);
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

        // Act
        var workflows = Directory.GetFiles(Path.Combine(root, ".github", "workflows"), "*.y*ml");

        // Assert
        workflows.ShouldNotBeEmpty();
        foreach (var workflow in workflows)
        {
            File.ReadAllText(workflow).ShouldNotContain("provenance/extraction-tools/", StringComparison.Ordinal);
        }
    }
}
