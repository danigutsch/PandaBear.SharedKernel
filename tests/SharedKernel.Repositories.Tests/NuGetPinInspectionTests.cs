namespace SharedKernel.Repositories.Tests;

/// <summary>Verifies package inspection rejects option-like input before invoking the SDK.</summary>
public sealed class NuGetPinInspectionTests
{
    [Theory]
    [InlineData("")]
    [InlineData("--version")]
    [InlineData("package with spaces")]
    public async Task Run_rejects_invalid_package_input_before_evaluating_the_checkout(string package)
    {
        // Arrange
        using var output = new InvariantStringWriter();
        using var error = new InvariantStringWriter();

        // Act
        var status = await NuGetPinInspection.Run(".", package, output, error, TestContext.Current.CancellationToken);

        // Assert
        status.ShouldBe(1);
        output.ToString().ShouldBeEmpty();
        error.ToString().ShouldContain("Specify a valid NuGet package ID.", StringComparison.Ordinal);
    }

    [Fact]
    public async Task Evidence_commands_report_when_the_checkout_cannot_be_evaluated()
    {
        // Arrange
        using var directory = new TemporaryDirectory();
        using var output = new InvariantStringWriter();
        using var error = new InvariantStringWriter();
        const string report = "dependency-comparison.json";
        var comparison = new NuGetRestoreGraphComparisonOptions(directory.Path, ".", report, null, null);
        var ct = TestContext.Current.CancellationToken;

        // Act
        var inspected = await NuGetPinInspection.Run(directory.Path, "Valid.Package", output, error, ct);
        var compared = await NuGetRestoreGraphComparison.Run(comparison, output, error, ct);

        // Assert
        inspected.ShouldBe(1);
        compared.ShouldBe(1);
        var diagnostics = error.ToString();
        diagnostics.ShouldContain("Could not inspect the restored package graph.", StringComparison.Ordinal);
        diagnostics.ShouldContain("Could not evaluate the restored solutions or write the comparison report.", StringComparison.Ordinal);
        File.Exists(Path.Combine(directory.Path, report)).ShouldBeFalse();
    }
}
