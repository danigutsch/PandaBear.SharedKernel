namespace SharedKernel.Repository.Tests;

/// <summary>Guards the <c>verify-restore-warnings</c> command, which fails on NuGet vulnerability warnings of every severity and on audits that could not run, and reports the rest.</summary>
public sealed class RestoreWarningVerificationTests
{
    private const string High = "/r/src/A/A.csproj : warning NU1903: Package 'System.Foo' 1.0.0 has a known high severity vulnerability, https://github.com/advisories/GHSA-aaaa-bbbb-cccc [/r/App.slnx]";
    private const string Critical = "/r/src/B/B.csproj : warning NU1904: Package 'System.Bar' 2.1.0 has a known critical severity vulnerability, https://github.com/advisories/GHSA-dddd-eeee-ffff [/r/App.slnx]";
    private const string Moderate = "/r/src/A/A.csproj : warning NU1902: Package 'System.Baz' 3.0.0 has a known moderate severity vulnerability, https://github.com/advisories/GHSA-gggg-hhhh-iiii [/r/App.slnx]";
    private const string Low = "/r/src/A/A.csproj : warning NU1901: Package 'System.Qux' 4.0.0 has a known low severity vulnerability, https://github.com/advisories/GHSA-jjjj-kkkk-llll [/r/App.slnx]";
    private const string UnknownSeverity = "/r/src/A/A.csproj : warning NU1900: Package 'Contoso.Utilities' 1.0.0 has a known unknown severity vulnerability, https://cve.contoso.com/advisories/1 [/r/App.slnx]";
    private const string DataError = "/r/src/A/A.csproj : warning NU1900: Error occurred while getting package vulnerability data: Unable to load the service index for source https://feed.example/v3/index.json. [/r/App.slnx]";
    private const string NoData = "/r/src/A/A.csproj : warning NU1905: Audit source 'nuget.org' did not provide any vulnerability data. [/r/App.slnx]";
    private const string NoDataFromUrl = "/r/src/A/A.csproj : warning NU1905: Audit source 'https://api.nuget.org/v3/index.json' did not provide any vulnerability data. [/r/App.slnx]";
    private const string NoDataFromCredentials = "/r/src/A/A.csproj : warning NU1905: Audit source 'https://user:token@feed.example/v3/index.json' did not provide any vulnerability data. [/r/App.slnx]";
    private const string Compatibility = "/r/src/A/A.csproj : warning NU1701: Package 'Legacy' 1.0.0 was restored using '.NETFramework,Version=v4.6.1' [/r/App.slnx]";
    private const string Approximation = "/r/src/A/A.csproj : warning NU1603: A 1.0.0 depends on Dep (>= 1.0.0) but Dep 1.0.0 was not found. Dep 1.0.1 was resolved instead. [/r/App.slnx]";

    [Theory]
    [InlineData(Low, "NU1901 System.Qux 4.0.0")]
    [InlineData(Moderate, "NU1902 System.Baz 3.0.0")]
    [InlineData(High, "NU1903 System.Foo 1.0.0")]
    [InlineData(Critical, "NU1904 System.Bar 2.1.0")]
    [InlineData(UnknownSeverity, "NU1900 Contoso.Utilities 1.0.0")]
    public async Task Vulnerabilities_of_every_severity_fail_with_a_remediation_and_a_summary(string warning, string finding)
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        using var scope = new RepositoryTestScope();
        await File.WriteAllTextAsync(Path.Combine(scope.Root, "restore.log"), $"Restore complete\n{warning}\n", ct);
        var processes = new ProcessLog();

        // Act
        var result = await RepositoryToolInvocation.Run(scope.Root, ["verify-restore-warnings", "--log", "restore.log", "--summary", "summary.md"], processes.Run, ct);
        var summary = await File.ReadAllTextAsync(Path.Combine(scope.Root, "summary.md"), ct);

        // Assert
        result.ExitCode.ShouldBe(2);
        result.Error.ShouldContain(finding, StringComparison.Ordinal);
        result.Error.ShouldContain("Update the package or record a reviewed exception", StringComparison.Ordinal);
        summary.ShouldContain("| Blocking | 1 |", StringComparison.Ordinal);
        summary.ShouldContain($"- {finding}", StringComparison.Ordinal);
        processes.Calls.ShouldBeEmpty();
    }

    [Theory]
    [InlineData(DataError, "NU1900 source")]
    [InlineData(NoData, "NU1905 source nuget.org")]
    [InlineData(NoDataFromUrl, "NU1905 source https://api.nuget.org/v3/index.json")]
    [InlineData(NoDataFromCredentials, "NU1905 source")]
    public async Task An_audit_that_could_not_run_fails_and_names_only_its_source(string warning, string finding)
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        using var scope = new RepositoryTestScope();
        await File.WriteAllTextAsync(Path.Combine(scope.Root, "restore.log"), $"Restore complete\n{warning}\n", ct);
        var processes = new ProcessLog();

        // Act
        var result = await RepositoryToolInvocation.Run(scope.Root, ["verify-restore-warnings", "--log", "restore.log", "--summary", "summary.md", "--report", "report.json"], processes.Run, ct);
        var summary = await File.ReadAllTextAsync(Path.Combine(scope.Root, "summary.md"), ct);
        var combined = await RepositoryToolInvocation.Run(scope.Root, ["warning-summary", "--report", "report.json"], processes.Run, ct);

        // Assert
        result.ExitCode.ShouldBe(2);
        result.Error.ShouldContain($"- {finding}\n", StringComparison.Ordinal);
        result.Error.ShouldContain("the vulnerability audit could not run", StringComparison.Ordinal);
        summary.ShouldContain($"- {finding}\n", StringComparison.Ordinal);
        combined.ExitCode.ShouldBe(0);
        combined.Output.ShouldContain($"- NuGet restore: {finding}", StringComparison.Ordinal);
        result.Error.ShouldNotContain("feed.example", StringComparison.Ordinal, "The rest of the warning text comes from the network or the configuration.");
        summary.ShouldNotContain("feed.example", StringComparison.Ordinal);
        processes.Calls.ShouldBeEmpty();
    }

    [Fact]
    public async Task Audit_failures_repeated_by_many_projects_count_once_per_source()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var otherSourceInOtherProject = DataError.Replace("feed.example", "other.example", StringComparison.Ordinal).Replace("/A/A.csproj", "/C/C.csproj", StringComparison.Ordinal);
        var sameSourceInOtherProject = NoData.Replace("/A/A.csproj", "/C/C.csproj", StringComparison.Ordinal);
        var log = string.Join('\n', DataError, otherSourceInOtherProject, NoData, sameSourceInOtherProject, NoDataFromUrl) + "\n";

        // Act
        var (result, report) = await WarningToolRuns.Verify(log, null, ct);

        // Assert
        result.ExitCode.ShouldBe(2);
        report.ShouldBe(new WarningReportView(3, 0, 0));
    }

    [Fact]
    public async Task Restore_warnings_that_are_not_vulnerabilities_stay_advisory_and_visible()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        using var scope = new RepositoryTestScope();
        await File.WriteAllTextAsync(Path.Combine(scope.Root, "restore.log"), $"{Compatibility}\n{Approximation}\n", ct);
        var processes = new ProcessLog();

        // Act
        var result = await RepositoryToolInvocation.Run(scope.Root, ["verify-restore-warnings", "--log", "restore.log", "--summary", "summary.md"], processes.Run, ct);
        var summary = await File.ReadAllTextAsync(Path.Combine(scope.Root, "summary.md"), ct);

        // Assert
        result.ExitCode.ShouldBe(0);
        result.Error.ShouldBe(string.Empty);
        summary.ShouldContain("| Blocking | 0 |", StringComparison.Ordinal);
        summary.ShouldContain("| Advisory | 2 |", StringComparison.Ordinal);
    }

    [Fact]
    public async Task Repeated_warnings_from_many_projects_count_once_and_summaries_append()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        using var scope = new RepositoryTestScope();
        var summaryPath = Path.Combine(scope.Root, "summary.md");
        await File.WriteAllTextAsync(summaryPath, "earlier step\n", ct);
        await File.WriteAllTextAsync(Path.Combine(scope.Root, "restore.log"), $"{Compatibility}\n{Compatibility.Replace("/A/A.csproj", "/C/C.csproj", StringComparison.Ordinal)}\n", ct);
        var processes = new ProcessLog();

        // Act
        var result = await RepositoryToolInvocation.Run(scope.Root, ["verify-restore-warnings", "--log", "restore.log", "--summary", "summary.md"], processes.Run, ct);
        var summary = await File.ReadAllTextAsync(summaryPath, ct);

        // Assert
        result.ExitCode.ShouldBe(0);
        summary.ShouldStartWith("earlier step\n", StringComparison.Ordinal);
        summary.ShouldContain("| Advisory | 1 |", StringComparison.Ordinal);
    }

    [Fact]
    public async Task Clean_log_passes_and_missing_log_fails()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        using var scope = new RepositoryTestScope();
        await File.WriteAllTextAsync(Path.Combine(scope.Root, "restore.log"), "Restore complete (1.0s)\n", ct);
        var processes = new ProcessLog();

        // Act
        var clean = await RepositoryToolInvocation.Run(scope.Root, ["verify-restore-warnings", "--log", "restore.log"], processes.Run, ct);
        var missing = await RepositoryToolInvocation.Run(scope.Root, ["verify-restore-warnings", "--log", "absent.log"], processes.Run, ct);

        // Assert
        clean.ExitCode.ShouldBe(0);
        missing.ExitCode.ShouldBe(2);
    }
}
