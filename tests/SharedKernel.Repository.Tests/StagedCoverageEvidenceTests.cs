namespace PandaBear.Repository.Tests;

/// <summary>Verifies the source and bytes of reports collected for strict staged measurement.</summary>
public sealed class StagedCoverageEvidenceTests
{
    [Fact]
    public async Task Combines_reports_captured_from_the_same_tree_in_separate_directories()
    {
        // Arrange
        using var scenario = new StagedCoverageEvidenceScenario();
        var first = await scenario.Capture("TestResults/first", 1, 2);
        var second = await scenario.Capture("TestResults/second", 3, 4, 5);
        first.ExitCode.ShouldBe(0, first.Error);
        second.ExitCode.ShouldBe(0, second.Error);

        // Act
        var result = await scenario.Measure("TestResults/first", "TestResults/second");

        // Assert
        result.ExitCode.ShouldBe(0, result.Error);
        result.Output.ShouldContain("5/5 changed executable lines and conditions.", StringComparison.Ordinal);
        scenario.Processes.Commands.ShouldContain(static command => command.Contains("--no-incremental", StringComparison.Ordinal));
        scenario.Processes.Commands.ShouldContain(static command => command.EndsWith(
            " HEAD " + StagedCoverageEvidenceScenario.CurrentTree + " --", StringComparison.Ordinal));
        scenario.Processes.Commands.ShouldNotContain(static command => command.Contains("--cached", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Measures_from_the_explicit_base_to_the_verified_capture_tree()
    {
        // Arrange
        using var scenario = new StagedCoverageEvidenceScenario();
        var capture = await scenario.Capture("TestResults/current", 1, 2, 3, 4, 5);
        capture.ExitCode.ShouldBe(0, capture.Error);

        // Act
        var result = await scenario.Invoke("coverage", "measure-staged", "--base", CoverageTestScope.BaseRevision,
            "--evidence", "TestResults/current/execution-library/results/coverage-evidence.json");

        // Assert
        result.ExitCode.ShouldBe(0, result.Error);
        result.Output.ShouldContain("5/5 changed executable lines and conditions.", StringComparison.Ordinal);
        scenario.Processes.Commands.ShouldContain(static command => command.EndsWith(
            " " + CoverageTestScope.BaseRevision + " " + StagedCoverageEvidenceScenario.CurrentTree + " --", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Rejects_an_earlier_capture_with_the_same_source_filename_before_counting_coverage()
    {
        // Arrange
        using var scenario = new StagedCoverageEvidenceScenario();
        scenario.Tree = StagedCoverageEvidenceScenario.PreviousTree;
        var previous = await scenario.Capture("TestResults/earlier", 3, 4, 5);
        scenario.Tree = StagedCoverageEvidenceScenario.CurrentTree;
        var current = await scenario.Capture("TestResults/later", 1, 2);
        previous.ExitCode.ShouldBe(0, previous.Error);
        current.ExitCode.ShouldBe(0, current.Error);

        // Act
        var result = await scenario.Measure("TestResults/later", "TestResults/earlier");

        // Assert
        result.ExitCode.ShouldBe(2);
        result.Error.ShouldContain("earlier", StringComparison.Ordinal);
        result.Error.ShouldContain("staged tree", StringComparison.Ordinal);
        result.Output.ShouldNotContain("Staged coverage measured", StringComparison.Ordinal);
    }

    [Fact]
    public async Task Rejects_a_coverage_report_changed_after_collection()
    {
        // Arrange
        using var scenario = new StagedCoverageEvidenceScenario();
        var capture = await scenario.Capture("TestResults/recorded", 1);
        capture.ExitCode.ShouldBe(0, capture.Error);
        scenario.ChangeReport("TestResults/recorded");

        // Act
        var result = await scenario.Measure("TestResults/recorded");

        // Assert
        result.ExitCode.ShouldBe(2);
        result.Error.ShouldContain("coverage.cobertura.1.xml", StringComparison.Ordinal);
        result.Error.ShouldContain("does not match", StringComparison.Ordinal);
        result.Output.ShouldNotContain("Staged coverage measured", StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_source_tree_change_during_capture_leaves_no_evidence()
    {
        // Arrange
        using var scenario = new StagedCoverageEvidenceScenario
        {
            TreeAfterTest = StagedCoverageEvidenceScenario.PreviousTree
        };

        // Act
        var result = await scenario.Capture("TestResults/edited", 1);

        // Assert
        result.ExitCode.ShouldBe(2);
        result.Error.ShouldContain("staged tree changed", StringComparison.Ordinal);
        scenario.HasEvidence("TestResults/edited").ShouldBeFalse();
    }

    [Fact]
    public async Task Refuses_capture_from_unstaged_tracked_files_before_building()
    {
        // Arrange
        using var scenario = new StagedCoverageEvidenceScenario
        {
            Unstaged = true
        };

        // Act
        var result = await scenario.Capture("TestResults/unstaged", 1);

        // Assert
        result.ExitCode.ShouldBe(2);
        result.Error.ShouldContain("unstaged changes", StringComparison.Ordinal);
        scenario.Processes.Commands.ShouldNotContain(static command => command.StartsWith("dotnet ", StringComparison.Ordinal));
        scenario.HasEvidence("TestResults/unstaged").ShouldBeFalse();
    }

    [Theory]
    [InlineData("null", "must contain a JSON object")]
    [InlineData("{}", "required")]
    [InlineData("{\"tree\":\"1111111111111111111111111111111111111111\",\"reports\":{}}", "names no coverage reports")]
    [InlineData("{\"tree\":\"1111111111111111111111111111111111111111\",\"reports\":{\"../outside.cobertura.xml\":\"bad\"}}", "invalid coverage report path")]
    public async Task Malformed_evidence_fails_without_emitting_coverage_counts(string evidence, string expectedError)
    {
        // Arrange
        using var scenario = new StagedCoverageEvidenceScenario();
        scenario.ChangeEvidence("TestResults/invalid", evidence);

        // Act
        var result = await scenario.Measure("TestResults/invalid");

        // Assert
        result.ExitCode.ShouldBe(2);
        result.Error.ShouldContain(expectedError, StringComparison.Ordinal);
        result.Output.ShouldNotContain("Staged coverage measured", StringComparison.Ordinal);
    }

    [Fact]
    public async Task Evidence_options_require_a_coverage_run_and_cannot_mix_with_raw_reports()
    {
        // Arrange
        using var scenario = new StagedCoverageEvidenceScenario();

        // Act
        var run = await scenario.Invoke("run", "--coverage-evidence");
        var measure = await scenario.Invoke("coverage", "measure-staged", "--report", "another.xml", "--evidence", "capture.json");

        // Assert
        run.ExitCode.ShouldBe(1);
        run.Error.ShouldContain("--coverage-evidence requires --coverage", StringComparison.Ordinal);
        measure.ExitCode.ShouldBe(1);
        measure.Error.ShouldContain("Specify either --report or --evidence", StringComparison.Ordinal);
        scenario.Processes.Calls.ShouldBeEmpty();
    }
}
