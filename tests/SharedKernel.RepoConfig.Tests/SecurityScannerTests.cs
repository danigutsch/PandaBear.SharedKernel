using System.Globalization;

namespace PandaBear.RepoConfig.Tests;

/// <summary>Tests the security scanner's exit codes, report publication, and handling of cancellation, a missing scanner, and unsafe report locations.</summary>
[Trait(TestTraitNames.CategoryName, TestTraits.CommandLineCategory)]
public sealed class SecurityScannerTests
{
    /// <summary>When the report directory is a symbolic link to another directory, the scan returns 3 and writes no <c>gitleaks.sarif</c> at the target.</summary>
    [Fact]
    public async Task Scan_rejects_symbolic_link_report_directories()
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        using var outside = new TemporaryRepoConfigWorkspace();
        Directory.CreateSymbolicLink(Path.Combine(workspace.RootPath, "reports"), outside.RootPath);
        using var output = new StringWriter(CultureInfo.InvariantCulture);
        using var error = new StringWriter(CultureInfo.InvariantCulture);
        var process = new SecurityScannerTestProcess(0, """{"version":"2.1.0","runs":[{"results":[]}]}""");

        // Act
        var result = await SecurityScanner.Run(workspace.RootPath, Path.Combine(workspace.RootPath, "reports"), output, error,
            Path.Combine(workspace.RootPath, "gitleaks"), process.Run, TestContext.Current.CancellationToken);

        // Assert
        result.ShouldBe(3);
        outside.FileExists("gitleaks.sarif").ShouldBeFalse();
    }

    /// <summary>With an already cancelled token, the scan returns 3, reports that it was canceled, and publishes no SARIF report.</summary>
    [Fact]
    public async Task Scan_reports_cancellation_without_publishing_a_report()
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        using var output = new StringWriter(CultureInfo.InvariantCulture);
        using var error = new StringWriter(CultureInfo.InvariantCulture);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        var process = new SecurityScannerTestProcess(0, null);

        // Act
        var result = await SecurityScanner.Run(workspace.RootPath, Path.Combine(workspace.RootPath, "reports"), output, error,
            Path.Combine(workspace.RootPath, "gitleaks"), process.Run, cancellation.Token);

        // Assert
        result.ShouldBe(3);
        error.ToString().ShouldContain("canceled", StringComparison.Ordinal);
        workspace.FileExists("reports/gitleaks.sarif").ShouldBeFalse();
    }

    /// <summary>The scan returns 0 for a clean report and 1 for scanner exit 10 with a finding, returns 3 and publishes no report for a missing, malformed, or inconsistent report or for scanner exit 1 or 126, and always runs the scanner without a shell, with redaction, on the repository root.</summary>
    /// <param name="scannerExit">The exit code the fake scanner returns.</param>
    /// <param name="report">The SARIF text the fake scanner writes, or <see langword="null"/> for none.</param>
    /// <param name="expected">The expected scan result.</param>
    [Theory]
    [InlineData(0, "{\"version\":\"2.1.0\",\"runs\":[{\"results\":[]}]}", 0)]
    [InlineData(10, "{\"version\":\"2.1.0\",\"runs\":[{\"results\":[{}]}]}", 1)]
    [InlineData(0, null, 3)]
    [InlineData(0, "not-json", 3)]
    [InlineData(0, "{\"version\":\"2.1.0\",\"runs\":[{\"results\":[{}]}]}", 3)]
    [InlineData(1, null, 3)]
    [InlineData(126, null, 3)]
    public async Task Scan_distinguishes_findings_from_scanner_and_report_failures(int scannerExit, string? report, int expected)
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        using var output = new StringWriter(CultureInfo.InvariantCulture);
        using var error = new StringWriter(CultureInfo.InvariantCulture);
        var process = new SecurityScannerTestProcess(scannerExit, report);

        // Act
        var result = await SecurityScanner.Run(workspace.RootPath, Path.Combine(workspace.RootPath, "reports with spaces"), output, error,
            Path.Combine(workspace.RootPath, "gitleaks"), process.Run, TestContext.Current.CancellationToken);

        // Assert
        result.ShouldBe(expected);
        workspace.FileExists("reports with spaces/gitleaks.sarif").ShouldBe(expected != 3);
        var invocation = process.StartInfo.ShouldNotBeNull();
        invocation.UseShellExecute.ShouldBeFalse();
        invocation.ArgumentList.ShouldContain("--redact=100");
        invocation.ArgumentList[1].ShouldBe(".");
        invocation.WorkingDirectory.ShouldBe(workspace.RootPath);
    }

    /// <summary>With no scanner executable, the scan returns 3, reports that the scanner is not installed, and starts no process.</summary>
    [Fact]
    public async Task Scan_fails_when_the_scanner_is_missing()
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        using var output = new StringWriter(CultureInfo.InvariantCulture);
        using var error = new StringWriter(CultureInfo.InvariantCulture);
        var process = new SecurityScannerTestProcess(0, null);

        // Act
        var result = await SecurityScanner.Run(workspace.RootPath, Path.Combine(workspace.RootPath, "reports"), output, error,
            null, process.Run, TestContext.Current.CancellationToken);

        // Assert
        result.ShouldBe(3);
        error.ToString().ShouldContain("not installed", StringComparison.Ordinal);
        process.StartInfo.ShouldBeNull();
    }

    /// <summary>When <c>reports/gitleaks.sarif</c> already exists, the scan returns 3 and leaves its content as "retained evidence".</summary>
    [Fact]
    public async Task Scan_does_not_overwrite_an_existing_report()
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        workspace.WriteFile("reports/gitleaks.sarif", "retained evidence");
        using var output = new StringWriter(CultureInfo.InvariantCulture);
        using var error = new StringWriter(CultureInfo.InvariantCulture);
        var process = new SecurityScannerTestProcess(0, """{"version":"2.1.0","runs":[{"results":[]}]}""");

        // Act
        var result = await SecurityScanner.Run(workspace.RootPath, Path.Combine(workspace.RootPath, "reports"), output, error,
            Path.Combine(workspace.RootPath, "gitleaks"), process.Run, TestContext.Current.CancellationToken);

        // Assert
        result.ShouldBe(3);
        workspace.ReadFile("reports/gitleaks.sarif").ShouldBe("retained evidence");
    }
}
