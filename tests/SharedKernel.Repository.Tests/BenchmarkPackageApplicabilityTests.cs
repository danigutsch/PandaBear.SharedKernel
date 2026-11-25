using SharedKernel.Repository.Tests.Conventions;

namespace SharedKernel.Repository.Tests;

/// <summary>Verifies the opt-in package boundary for changed benchmark sources.</summary>
public static class BenchmarkPackageApplicabilityTests
{
    private const string Exclusion = "benchmarks/**/*.cs";
    private const string BenchmarkProject = "benchmarks/Probe.Benchmarks/Probe.Benchmarks.csproj";
    private const string BenchmarkSource = "benchmarks/Probe.Benchmarks/Probe.cs";
    private const string AdditionalSource = "benchmarks/Probe.Benchmarks/Additional.cs";

    [Theory]
    [InlineData("edit", false)]
    [InlineData("add", true)]
    [InlineData("remove", true)]
    [InlineData("rename", true)]
    [InlineData("mode", true)]
    [InlineData("mixed", true)]
    [InlineData("build-input", true)]
    [InlineData("full", true)]
    [InlineData("unavailable-base", true)]
    [InlineData("uncertain-graph", true)]
    [InlineData("selected-tests", true)]
    [InlineData("missing-worktree-file", true)]
    [InlineData("dirty-worktree", true)]
    [InlineData("unsupported-pattern", true)]
    [InlineData("no-option", true)]
    public static async Task Candidate_selection_accepts_only_existing_regular_benchmark_source_edits(string change, bool candidateRequired)
    {
        // Arrange
        using var repository = new RepositoryTestScope();
        var ct = TestContext.Current.CancellationToken;
        repository.AddProject(BenchmarkProject, """<Project><PropertyGroup><IsPackable>false</IsPackable></PropertyGroup></Project>""");
        repository.WriteFile(BenchmarkSource, "before");
        repository.WriteFile("src/Core/Probe.cs", "before");
        if (change == "uncertain-graph")
        {
            repository.AddProject("src/Uncertain/Uncertain.csproj", "Invalid project XML");
        }

        if (change == "selected-tests")
        {
            repository.Configure("""[{ "name": "library", "projects": [] }]""",
                alwaysSelected: """["tests/Core.Tests/Core.Tests.csproj"]""");
        }

        await GitCheckout.Init(repository.Root, ct);
        await RealProcess.Run("git", ["config", "core.filemode", "false"], repository.Root, ct);
        await GitCheckout.CommitAll(repository.Root, ct);
        var baseline = (await RealProcess.Run("git", ["rev-parse", "HEAD"], repository.Root, ct)).Trim();
        var benchmark = Path.Combine(repository.Root, BenchmarkSource);
        switch (change)
        {
            case "add":
                repository.WriteFile(AdditionalSource, "added");
                break;
            case "remove":
                File.Delete(benchmark);
                break;
            case "rename":
                File.Move(benchmark, Path.Combine(repository.Root, AdditionalSource));
                break;
            case "mode":
                await RealProcess.Run("git", ["update-index", "--chmod=+x", "--", BenchmarkSource], repository.Root, ct);
                break;
            default:
                repository.WriteFile(BenchmarkSource, "after");
                break;
        }

        if (change == "mixed")
        {
            repository.WriteFile("src/Core/Probe.cs", "after");
        }

        if (change == "build-input")
        {
            repository.WriteFile("Directory.Build.props", "<Project />");
        }

        await GitCheckout.CommitAll(repository.Root, ct);
        if (change == "missing-worktree-file")
        {
            File.Delete(benchmark);
        }

        if (change == "dirty-worktree")
        {
            repository.WriteFile("Directory.Build.targets", "<Project />");
        }

        var comparison = change switch
        {
            "full" => string.Empty,
            "unavailable-base" => new string('f', 40),
            _ => baseline,
        };
        var outputPath = Path.Combine(repository.Root, "github-output.txt");
        string[] arguments = ["select", "--base", comparison, "--github-output", outputPath];
        if (change != "no-option")
        {
            var exclusion = change == "unsupported-pattern" ? "**/*.cs" : Exclusion;
            arguments = [.. arguments, "--candidate-excluded-source", exclusion];
        }

        // Act
        var result = await RepositoryToolInvocation.Run(repository.Root, arguments, GitCheckout.RealGit(new ProcessLog()), ct);

        // Assert
        result.ExitCode.ShouldBe(0, result.Error);
        var outputs = await File.ReadAllTextAsync(outputPath, ct);
        outputs.ShouldContain("build_required=true\n", StringComparison.Ordinal);
        if (change == "no-option")
        {
            outputs.ShouldNotContain("candidate_required=", StringComparison.Ordinal);
        }
        else
        {
            outputs.ShouldContain(candidateRequired ? "candidate_required=true\n" : "candidate_required=false\n", StringComparison.Ordinal);
        }

        if (!candidateRequired)
        {
            var selection = Path.Combine(repository.Root, "TestResults", "selection");
            var selectedProjects = await SelectionTestFiles.Projects(selection, "affected.build", ct);
            selectedProjects.ShouldBe([BenchmarkProject]);
            outputs.ShouldContain("matrix=[]\n", StringComparison.Ordinal);
        }
    }

    [Fact]
    public static async Task Candidate_applicability_cannot_overwrite_an_existing_slice_output()
    {
        // Arrange
        using var repository = new RepositoryTestScope();
        var ct = TestContext.Current.CancellationToken;
        repository.Configure("""[{ "name": "candidate", "projects": [] }]""", defaultSlice: "candidate");
        var outputPath = Path.Combine(repository.Root, "github-output.txt");

        // Act
        var result = await RepositoryToolInvocation.Run(repository.Root,
            ["select", "--candidate-excluded-source", Exclusion, "--github-output", outputPath], new ProcessLog().Run, ct);

        // Assert
        result.ExitCode.ShouldBe(2);
        result.Error.ShouldContain("candidate_required is already used by the candidate test slice.", StringComparison.Ordinal);
        File.Exists(outputPath).ShouldBeFalse();
    }

    [Fact]
    public static void Hosted_package_skip_preserves_full_candidate_inputs_and_required_validation()
    {
        // Arrange
        var root = PackagingConventionTestFiles.GetRepositoryRoot();
        var workflow = File.ReadAllText(Path.Combine(root, ".github", "workflows", "ci.yml"));

        // Act
        var selection = WorkflowJobs.Read(workflow, "selection");
        var candidate = WorkflowJobs.Read(workflow, "candidate");
        var linux = WorkflowJobs.Read(workflow, "linux-package-consumption");
        var windows = WorkflowJobs.Read(workflow, "windows-package-consumption");
        var aggregate = WorkflowJobs.Read(workflow, "validation-summary");

        // Assert
        selection.ShouldContain("candidate_required: ${{ steps.selection.outputs.candidate_required }}", StringComparison.Ordinal);
        selection.ShouldContain("--candidate-excluded-source \"" + Exclusion + "\"", StringComparison.Ordinal);
        candidate.ShouldContain("needs.selection.outputs.build_required == 'true'", StringComparison.Ordinal);
        candidate.ShouldContain("github.event_name != 'pull_request'", StringComparison.Ordinal);
        candidate.ShouldContain("needs.selection.outputs.full_validation != 'false'", StringComparison.Ordinal);
        candidate.ShouldContain("needs.selection.outputs.matrix != '[]'", StringComparison.Ordinal);
        candidate.ShouldContain("needs.selection.outputs.candidate_required != 'false'", StringComparison.Ordinal);
        candidate.ShouldContain("fetch-depth: 0", StringComparison.Ordinal);
        candidate.ShouldNotContain("sparse-checkout:", StringComparison.Ordinal);
        linux.ShouldContain("needs: candidate", StringComparison.Ordinal);
        linux.ShouldContain("consume-candidate --candidate-directory artifacts/candidate --aot", StringComparison.Ordinal);
        windows.ShouldContain("needs: candidate", StringComparison.Ordinal);
        windows.ShouldContain("Validate Windows setup and workflow contracts", StringComparison.Ordinal);
        windows.ShouldContain("consume-candidate --candidate-directory artifacts/candidate --aot", StringComparison.Ordinal);
        aggregate.ShouldContain("'SharedKernel Validation'", StringComparison.Ordinal);
        aggregate.ShouldContain("CANDIDATE_REQUIRED: ${{ needs.selection.outputs.candidate_required }}", StringComparison.Ordinal);
        aggregate.ShouldContain("FULL_VALIDATION: ${{ needs.selection.outputs.full_validation }}", StringComparison.Ordinal);
        aggregate.ShouldContain("expected_package_result=success", StringComparison.Ordinal);
        aggregate.ShouldContain(
            "[[ \"$EVENT_NAME\" == pull_request && \"$FULL_VALIDATION\" == false && \"$MATRIX\" == '[]' && \"$CANDIDATE_REQUIRED\" == false ]]",
            StringComparison.Ordinal);
        aggregate.ShouldContain("expected_package_result=skipped", StringComparison.Ordinal);
        aggregate.ShouldContain(
            "for result in \"$CANDIDATE_RESULT\" \"$LINUX_RESULT\" \"$WINDOWS_RESULT\"; do\n"
                + "              test \"$result\" = \"$expected_package_result\" || exit 1\n",
            StringComparison.Ordinal);
    }
}
