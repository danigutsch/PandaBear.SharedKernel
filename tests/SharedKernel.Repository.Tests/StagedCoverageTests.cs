namespace PandaBear.Repository.Tests;

/// <summary>Verifies local coverage reporting for the Git index.</summary>
public sealed class StagedCoverageTests
{
    [Fact]
    public async Task Measures_only_staged_lines_and_merges_reports_with_project_relative_sources()
    {
        // Arrange
        using var scope = new CoverageTestScope(0);
        scope.Patch = "--- a/src/Core.cs\n+++ b/src/Core.cs\n@@ -1 +1,3 @@\n-old\n+one\n+two\n+three\n";
        scope.WriteFile("coverage.xml", CoverageTestScope.Report("src/Core.cs", [(1, false, "50% (1/2)"), (2, false, null), (3, false, null), (4, true, null)]));
        var projectReport = CoverageTestScope.Report("Core.cs", [(1, true, null), (2, true, null)])
            .Replace("<coverage>", "<coverage><sources><source>src</source></sources>", StringComparison.Ordinal);
        scope.WriteFile("reports/independent.cobertura.xml", projectReport);
        var processes = new ProcessLog((_, arguments, _) => Task.FromResult(arguments.Contains("--cached") ? scope.Patch! : string.Empty));

        // Act
        var result = await scope.MeasureStaged(processes, "coverage.xml", "reports");

        // Assert
        result.ExitCode.ShouldBe(0, result.Error);
        result.Output.ShouldContain("src/Core.cs 2/3 executable lines, 1/2 conditions.", StringComparison.Ordinal);
        result.Output.ShouldContain("3/5 changed executable lines and conditions.", StringComparison.Ordinal);
        processes.Commands.ShouldContain(static command => command.EndsWith(" --cached HEAD --", StringComparison.Ordinal));
        File.Exists(Path.Combine(scope.Root, "TestResults/coverage-gate.json")).ShouldBeFalse();
    }

    [Fact]
    public async Task Measures_the_staged_increment_above_an_explicit_git_tree()
    {
        // Arrange
        using var scope = new CoverageTestScope(4);
        const string upper = "--- a/src/Core.cs\n+++ b/src/Core.cs\n@@ -4,0 +5 @@\n+five\n";
        var processes = new ProcessLog((_, arguments, _) => Task.FromResult(arguments switch
        {
            [.., "--cached", CoverageTestScope.BaseRevision, "--"] => upper,
            [.., "--cached", "HEAD", "--"] => scope.Patch!,
            _ => string.Empty,
        }));

        // Act
        var result = await RepositoryToolInvocation.Run(scope.Root,
            ["coverage", "measure-staged", "--report", "coverage.xml", "--base", CoverageTestScope.BaseRevision],
            processes.Run, TestContext.Current.CancellationToken);

        // Assert
        result.ExitCode.ShouldBe(0, result.Error);
        result.Output.ShouldContain("0/1 changed executable lines and conditions.", StringComparison.Ordinal);
        processes.Commands.ShouldNotContain(static command => command.EndsWith(" --cached HEAD --", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Rejects_a_staged_base_that_is_not_a_full_git_object_ID()
    {
        // Arrange
        using var scope = new CoverageTestScope(5);
        var processes = scope.Processes;

        // Act
        var result = await RepositoryToolInvocation.Run(scope.Root,
            ["coverage", "measure-staged", "--report", "coverage.xml", "--base", "HEAD^"],
            processes.Run, TestContext.Current.CancellationToken);

        // Assert
        result.ExitCode.ShouldBe(2);
        result.Error.ShouldContain("--base requires a full hexadecimal Git object ID.", StringComparison.Ordinal);
        processes.Commands.ShouldBeEmpty();
    }

    [Fact]
    public async Task Reports_an_included_staged_file_without_coverage()
    {
        // Arrange
        using var scope = new CoverageTestScope(5);
        scope.WriteFile("coverage.xml", "<coverage />");
        var processes = new ProcessLog((_, arguments, _) => Task.FromResult(arguments.Contains("--cached") ? scope.Patch! : string.Empty));

        // Act
        var result = await scope.MeasureStaged(processes);

        // Assert
        result.ExitCode.ShouldBe(2);
        result.Error.ShouldContain("in no coverage report: src/Core.cs", StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("constant-symbols", 0, "src/Core.cs 0/0 executable lines, 0/0 conditions.")]
    [InlineData("stale-symbols", 2, "in no coverage report: src/Core.cs")]
    [InlineData("executable-symbols", 2, "in no coverage report: src/Core.cs")]
    public async Task Uses_release_symbols_to_classify_an_unreported_staged_file(
        string scenario, int expectedExit, string expectedMessage)
    {
        // Arrange
        using var scope = new CoverageTestScope(0);
        scope.WriteSymbols(scenario);
        var processes = new ProcessLog((_, arguments, _) => Task.FromResult(arguments.Contains("--cached") ? scope.Patch! : string.Empty));

        // Act
        var result = await scope.MeasureStaged(processes);

        // Assert
        result.ExitCode.ShouldBe(expectedExit, result.Error);
        var message = expectedExit == 0 ? result.Output : result.Error;
        message.ShouldContain(expectedMessage, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Rejects_tracked_unstaged_changes_before_reading_the_staged_diff()
    {
        // Arrange
        using var scope = new CoverageTestScope(5);
        var processes = new ProcessLog((_, _, _) => Task.FromResult("src/Core.cs\0"));

        // Act
        var result = await scope.MeasureStaged(processes);

        // Assert
        result.ExitCode.ShouldBe(2);
        result.Error.ShouldContain("unstaged", StringComparison.Ordinal);
        processes.Commands.ShouldNotContain(static command => command.Contains("--cached", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Excludes_generated_files_using_the_consumer_policy()
    {
        // Arrange
        using var scope = new CoverageTestScope(5);
        var patch = scope.Patch!.Replace("src/Core.cs", "src/Core.g.cs", StringComparison.Ordinal);
        var processes = new ProcessLog((_, arguments, _) => Task.FromResult(arguments.Contains("--cached") ? patch : string.Empty));

        // Act
        var result = await scope.MeasureStaged(processes);

        // Assert
        result.ExitCode.ShouldBe(0, result.Error);
        result.Output.ShouldContain("0/0 changed executable lines and conditions.", StringComparison.Ordinal);
        result.Output.ShouldNotContain("Core.g.cs", StringComparison.Ordinal);
    }

    [Fact]
    public async Task Resolves_report_paths_and_git_from_an_explicit_repository_root()
    {
        // Arrange
        using var scope = new CoverageTestScope(5);
        var parent = Path.GetDirectoryName(scope.Root)!;
        var processes = new ProcessLog((_, arguments, _) => Task.FromResult(arguments.Contains("--cached") ? scope.Patch! : string.Empty));

        // Act
        var result = await RepositoryToolInvocation.Run(
            parent,
            ["coverage", "measure-staged", "--root", scope.Root, "--report", "coverage.xml"],
            processes.Run,
            TestContext.Current.CancellationToken);

        // Assert
        result.ExitCode.ShouldBe(0, result.Error);
        result.Output.ShouldContain("5/5 changed executable lines and conditions.", StringComparison.Ordinal);
        processes.Calls.Select(static call => call.WorkingDirectory).ShouldBe([scope.Root, scope.Root]);
    }

    [Fact]
    public async Task Rejects_a_blank_report_path_before_invoking_git()
    {
        // Arrange
        using var scope = new CoverageTestScope(5);
        var processes = scope.Processes;

        // Act
        var result = await scope.MeasureStaged(processes, " ");

        // Assert
        result.ExitCode.ShouldBe(1);
        result.Error.ShouldContain("--report requires a path.", StringComparison.Ordinal);
        processes.Commands.ShouldBeEmpty();
    }
}
