namespace PandaBear.Repository.Tests;

/// <summary>Guards the <c>warning-summary</c> command, which combines warning reports into a Markdown table.</summary>
public sealed class WarningSummaryTests
{
    private const string Restore = """{"tool":"NuGet restore","blocking":1,"advisory":2,"accepted":0,"blockingFindings":["NU1903 System.Foo 1.0.0"]}""";
    private const string Lint = """{"tool":"Lint","blocking":0,"advisory":3,"accepted":4,"blockingFindings":[]}""";

    [Fact]
    public async Task Summary_lists_each_tool_with_totals_and_blocking_findings_and_appends()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        using var scope = new RepositoryTestScope();
        await File.WriteAllTextAsync(Path.Combine(scope.Root, "restore.json"), Restore, ct);
        await File.WriteAllTextAsync(Path.Combine(scope.Root, "lint.json"), Lint, ct);
        await File.WriteAllTextAsync(Path.Combine(scope.Root, "summary.md"), "earlier step\n", ct);
        var processes = new ProcessLog();

        // Act
        var result = await RepositoryToolInvocation.Run(scope.Root, ["warning-summary", "--report", "restore.json", "--report", "lint.json", "--summary", "summary.md"], processes.Run, ct);
        var summary = await File.ReadAllTextAsync(Path.Combine(scope.Root, "summary.md"), ct);

        // Assert
        result.ExitCode.ShouldBe(0);
        summary.ShouldStartWith("earlier step\n", StringComparison.Ordinal);
        summary.ShouldContain("| Lint | 0 | 3 | 4 |", StringComparison.Ordinal);
        summary.ShouldContain("| NuGet restore | 1 | 2 | 0 |", StringComparison.Ordinal);
        summary.ShouldContain("| **Total** | 1 | 5 | 4 |", StringComparison.Ordinal);
        summary.ShouldContain("- NuGet restore: NU1903 System.Foo 1.0.0", StringComparison.Ordinal);
        summary.IndexOf("| Lint |", StringComparison.Ordinal).ShouldBeLessThan(summary.IndexOf("| NuGet restore |", StringComparison.Ordinal));
        processes.Calls.ShouldBeEmpty();
    }

    [Fact]
    public async Task Summary_goes_to_standard_output_without_a_summary_file()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        using var scope = new RepositoryTestScope();
        await File.WriteAllTextAsync(Path.Combine(scope.Root, "lint.json"), Lint, ct);
        var processes = new ProcessLog();

        // Act
        var result = await RepositoryToolInvocation.Run(scope.Root, ["warning-summary", "--report", "lint.json"], processes.Run, ct);

        // Assert
        result.ExitCode.ShouldBe(0);
        result.Output.ShouldContain("| Lint | 0 | 3 | 4 |", StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("""{"tool":"Bad|Tool","blocking":0,"advisory":0,"accepted":0,"blockingFindings":[]}""")]
    [InlineData("""{"tool":"Lint","blocking":-1,"advisory":0,"accepted":0,"blockingFindings":[]}""")]
    [InlineData("""{"tool":"Lint","blocking":1,"advisory":0,"accepted":0,"blockingFindings":["line one\n- injected"]}""")]
    [InlineData("""{"tool":"Lint","blocking":0,"advisory":0,"accepted":0,"blockingFindings":[],"extra":1}""")]
    [InlineData("not json")]
    public async Task Malformed_or_unsafe_reports_fail_without_writing_a_summary(string report)
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        using var scope = new RepositoryTestScope();
        await File.WriteAllTextAsync(Path.Combine(scope.Root, "bad.json"), report, ct);
        var processes = new ProcessLog();

        // Act
        var result = await RepositoryToolInvocation.Run(scope.Root, ["warning-summary", "--report", "bad.json", "--summary", "summary.md"], processes.Run, ct);

        // Assert
        result.ExitCode.ShouldBe(2);
        File.Exists(Path.Combine(scope.Root, "summary.md")).ShouldBeFalse();
    }

    [Fact]
    public async Task Restore_verification_writes_a_report_that_the_summary_can_consume()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        using var scope = new RepositoryTestScope();
        const string Log = "/r/A.csproj : warning NU1903: Package 'System.Foo' 1.0.0 has a known high severity vulnerability, https://github.com/advisories/GHSA-aaaa-bbbb-cccc [/r/App.slnx]\n"
            + "/r/A.csproj : warning NU1901: Package 'System.Qux' 4.0.0 has a known low severity vulnerability, https://github.com/advisories/GHSA-jjjj-kkkk-llll [/r/App.slnx]\n";
        await File.WriteAllTextAsync(Path.Combine(scope.Root, "restore.log"), Log, ct);
        var processes = new ProcessLog();

        // Act
        var verify = await RepositoryToolInvocation.Run(scope.Root, ["verify-restore-warnings", "--log", "restore.log", "--report", "restore.json"], processes.Run, ct);
        var summary = await RepositoryToolInvocation.Run(scope.Root, ["warning-summary", "--report", "restore.json"], processes.Run, ct);

        // Assert
        verify.ExitCode.ShouldBe(2);
        summary.ExitCode.ShouldBe(0);
        summary.Output.ShouldContain("| NuGet restore | 1 | 1 | 0 |", StringComparison.Ordinal);
        summary.Output.ShouldContain("- NuGet restore: NU1903 System.Foo 1.0.0", StringComparison.Ordinal);
    }
}
