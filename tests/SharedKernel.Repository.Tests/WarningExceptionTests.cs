namespace PandaBear.Repository.Tests;

public sealed class WarningExceptionTests
{
    private const string High = "/r/src/A/A.csproj : warning NU1903: Package 'System.Foo' 1.0.0 has a known high severity vulnerability [/r/App.slnx]";
    private const string HighOther = "/r/src/A/A.csproj : warning NU1903: Package 'System.Foo' 1.1.0 has a known high severity vulnerability [/r/App.slnx]";
    private const string Low = "/r/src/A/A.csproj : warning NU1901: Package 'System.Qux' 4.0.0 has a known low severity vulnerability [/r/App.slnx]";

    [Fact]
    public async Task A_blocking_finding_inside_an_unexpired_exception_is_accepted()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;

        // Act
        var (result, report) = await WarningToolRuns.Verify(High, WarningToolRuns.Ledger("NU1903", "System.Foo", 1, "2999-01-01"), ct);

        // Assert
        result.ExitCode.ShouldBe(0);
        report.ShouldBe(new WarningReportView(0, 0, 1));
    }

    [Fact]
    public async Task An_advisory_finding_inside_an_exception_moves_from_advisory_to_accepted()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;

        // Act
        var (_, report) = await WarningToolRuns.Verify($"{Low}\n", WarningToolRuns.Ledger("NU1901", "System.Qux", 1, "2999-01-01"), ct);

        // Assert
        report.ShouldBe(new WarningReportView(0, 0, 1));
    }

    [Fact]
    public async Task Findings_above_the_baseline_keep_their_classification()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;

        // Act
        var (result, report) = await WarningToolRuns.Verify($"{High}\n{HighOther}\n", WarningToolRuns.Ledger("NU1903", "System.Foo", 1, "2999-01-01"), ct);

        // Assert
        result.ExitCode.ShouldBe(2);
        report.ShouldBe(new WarningReportView(1, 0, 1));
    }

    [Fact]
    public async Task A_finding_outside_the_scope_is_not_absorbed()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;

        // Act
        var (result, report) = await WarningToolRuns.Verify(High, WarningToolRuns.Ledger("NU1903", "System.Other", 5, "2999-01-01"), ct);

        // Assert
        result.ExitCode.ShouldBe(2);
        report.ShouldBe(new WarningReportView(1, 0, 0));
    }

    [Fact]
    public async Task An_expired_exception_stops_counting_and_is_reported_as_blocking()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;

        // Act
        var (result, report) = await WarningToolRuns.Verify($"{Low}\n", WarningToolRuns.Ledger("NU1901", "System.Qux", 1, "2000-01-01"), ct);

        // Assert
        result.ExitCode.ShouldBe(2);
        result.Error.ShouldContain("expired NU1901 System.Qux", StringComparison.Ordinal);
        report.ShouldBe(new WarningReportView(1, 1, 0));
    }

    [Theory]
    [InlineData("""{"exceptions":[{"tool":"NuGet restore","rule":"NU1903","scope":"System.Foo","baseline":1,"rationale":"x","owner":"o","expires":"2999-01-01","extra":1}]}""")]
    [InlineData("""{"exceptions":[{"tool":"NuGet restore","rule":"NU1903","scope":"System.Foo","baseline":-1,"rationale":"x","owner":"o","expires":"2999-01-01"}]}""")]
    [InlineData("""{"exceptions":[{"tool":"NuGet restore","rule":"NU1903","scope":"[x](https://evil)","baseline":1,"rationale":"x","owner":"o","expires":"2999-01-01"}]}""")]
    [InlineData("""{"exceptions":[{"tool":"NuGet restore","rule":"NU1903","scope":"System.Foo","baseline":1,"rationale":" ","owner":"o","expires":"2999-01-01"}]}""")]
    public async Task An_invalid_ledger_fails_without_writing_a_report(string ledger)
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;

        // Act
        var (result, report) = await WarningToolRuns.Verify(High, ledger, ct);

        // Assert
        result.ExitCode.ShouldNotBe(0);
        report.ShouldBe(default);
    }
}
