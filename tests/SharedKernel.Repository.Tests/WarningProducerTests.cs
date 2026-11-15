namespace PandaBear.Repository.Tests;

/// <summary>Guards the <c>warning-report</c> command, which writes a warning report for a step of the CI pipeline.</summary>
public sealed class WarningProducerTests
{
    [Fact]
    public async Task A_failed_step_writes_a_blocking_report_naming_the_tool()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;

        // Act
        var (result, report) = await WarningToolRuns.Produce(["--tool", "Markdown lint", "--outcome", "failure"], ct);

        // Assert
        result.ExitCode.ShouldBe(0);
        report.ShouldBe("""{"tool":"Markdown lint","blocking":1,"advisory":0,"accepted":0,"blockingFindings":["Markdown lint failed"]}""");
    }

    [Fact]
    public async Task A_passed_step_reports_its_advisory_count_only()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;

        // Act
        var (result, report) = await WarningToolRuns.Produce(["--tool", "Build", "--outcome", "success", "--advisory", "3"], ct);

        // Assert
        result.ExitCode.ShouldBe(0);
        report.ShouldBe("""{"tool":"Build","blocking":0,"advisory":3,"accepted":0,"blockingFindings":[]}""");
    }

    [Theory]
    [InlineData("skipped")]
    [InlineData("cancelled")]
    [InlineData("passed")]
    public async Task A_step_that_did_not_run_or_has_an_unknown_outcome_fails_without_a_report(string outcome)
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;

        // Act
        var (result, report) = await WarningToolRuns.Produce(["--tool", "Lint", "--outcome", outcome], ct);

        // Assert
        result.ExitCode.ShouldNotBe(0);
        report.ShouldBeNull();
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("1.5")]
    [InlineData("99999999999")]
    [InlineData("+")]
    public async Task An_advisory_count_that_is_not_a_whole_number_is_a_parse_error_without_a_report(string advisory)
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;

        // Act
        var (result, report) = await WarningToolRuns.Produce(["--tool", "Build", "--outcome", "success", "--advisory", advisory], ct);

        // Assert
        result.ExitCode.ShouldBe(1, "A value that is not a number is reported as a parse error, not thrown.");
        result.Error.ShouldContain("'" + advisory + "'", StringComparison.Ordinal);
        result.Error.ShouldContain("--advisory", StringComparison.Ordinal);
        report.ShouldBeNull();
    }

    [Fact]
    public async Task A_negative_advisory_count_fails_without_a_report()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;

        // Act
        var (result, report) = await WarningToolRuns.Produce(["--tool", "Build", "--outcome", "success", "--advisory", "-1"], ct);

        // Assert
        result.ExitCode.ShouldBe(1, result.Error);
        result.Error.ShouldContain("Option '--advisory' cannot be negative.", StringComparison.Ordinal);
        report.ShouldBeNull();
    }

    [Fact]
    public async Task A_tool_name_that_could_inject_markup_fails_without_a_report()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;

        // Act
        var (result, report) = await WarningToolRuns.Produce(["--tool", "[x](https://evil)", "--outcome", "success"], ct);

        // Assert
        result.ExitCode.ShouldNotBe(0);
        report.ShouldBeNull();
    }
}
