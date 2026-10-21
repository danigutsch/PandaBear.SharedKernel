using System.Globalization;

namespace PandaBear.RepoConfig.Tests;

[Trait(TestTraitNames.CategoryName, TestTraits.CommandLineCategory)]
public sealed class SecurityScannerTests
{
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
        invocation.ArgumentList[1].ShouldBe(workspace.RootPath);
    }

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
