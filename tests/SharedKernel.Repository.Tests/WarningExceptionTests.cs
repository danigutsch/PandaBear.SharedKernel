namespace SharedKernel.Repository.Tests;

/// <summary>Guards how the restore warning verification applies the exception ledger to its findings.</summary>
public sealed class WarningExceptionTests
{
    private const string High = "/r/src/A/A.csproj : warning NU1903: Package 'System.Foo' 1.0.0 has a known high severity vulnerability [/r/App.slnx]";
    private const string HighOther = "/r/src/A/A.csproj : warning NU1903: Package 'System.Foo' 1.1.0 has a known high severity vulnerability [/r/App.slnx]";
    private const string Compatibility = "/r/src/A/A.csproj : warning NU1701: Package 'Legacy' 1.0.0 was restored using '.NETFramework,Version=v4.6.1' [/r/App.slnx]";
    private const string DataError = "/r/src/A/A.csproj : warning NU1900: Error occurred while getting package vulnerability data: Unable to load the service index for source https://feed.example/v3/index.json. [/r/App.slnx]";
    private const string NoData = "/r/src/A/A.csproj : warning NU1905: Audit source 'nuget.org' did not provide any vulnerability data. [/r/App.slnx]";

    [Theory]
    [InlineData(DataError, "NU1900", "source")]
    [InlineData(NoData, "NU1905", "source nuget.org")]
    [InlineData(NoData, "NU1905", "source")]
    public async Task An_audit_that_could_not_run_is_accepted_by_an_unexpired_exception_for_its_source(string warning, string rule, string scope)
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;

        // Act
        var (result, report) = await WarningToolRuns.Verify($"{warning}\n", WarningToolRuns.Ledger(rule, scope, 1, "2999-01-01"), ct);

        // Assert
        result.ExitCode.ShouldBe(0);
        report.ShouldBe(new WarningReportView(0, 0, 1));
    }

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
        var (_, report) = await WarningToolRuns.Verify($"{Compatibility}\n", WarningToolRuns.Ledger("NU1701", "Legacy", 1, "2999-01-01"), ct);

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
        var (result, report) = await WarningToolRuns.Verify($"{Compatibility}\n", WarningToolRuns.Ledger("NU1701", "Legacy", 1, "2000-01-01"), ct);

        // Assert
        result.ExitCode.ShouldBe(2);
        result.Error.ShouldContain("expired NU1701 Legacy", StringComparison.Ordinal);
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
