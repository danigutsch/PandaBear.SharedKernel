namespace SharedKernel.Repositories.Tests;

/// <summary>Tests invalid trial requests before the SDK or Git workspace runs.</summary>
public sealed class NuGetTrialSelectionTests
{
    [Theory]
    [InlineData("empty", "Select a central pin, an explicit package update, or the SDK vulnerable mode.")]
    [InlineData("vulnerableWithUpdate", "Choose either explicit package updates or the SDK vulnerable mode.")]
    [InlineData("invalidPin", "The pin must be a NuGet package identifier.")]
    [InlineData("invalidProps", "Select the .props file owning the central declarations.")]
    [InlineData("proposeWithUpdate", "Use --propose with --remove-pin only")]
    [InlineData("coordinateWithoutProposal", "Use --coordinate with --propose")]
    [InlineData("invalidCoordinatedPackage", "Use --coordinate with --propose")]
    [InlineData("existingReport", "Choose new output paths for the patch and its report.")]
    public async Task Trial_rejects_conflicting_inputs_without_creating_a_patch(string scenario, string diagnostic)
    {
        // Arrange
        using var directory = new TemporaryDirectory();
        using var output = new InvariantStringWriter();
        using var error = new InvariantStringWriter();
        const string patch = "proposal.patch";
        var options = new NuGetUpgradeTrialOptions(directory.Path, patch, "Directory.Packages.props", "Package",
            [], false, [], false, false);
        options = scenario switch
        {
            "empty" => options with { RemovePin = null },
            "vulnerableWithUpdate" => options with { Updates = ["Package@2.0.0"], Vulnerable = true },
            "invalidPin" => options with { RemovePin = "-Package" },
            "invalidProps" => options with { PropsPath = "Directory.Packages.xml" },
            "proposeWithUpdate" => options with { ProposePinUpdates = true, Updates = ["Other@2.0.0"] },
            "coordinateWithoutProposal" => options with { CoordinatedPackages = ["Other"] },
            "invalidCoordinatedPackage" => options with { ProposePinUpdates = true, CoordinatedPackages = ["-Other"] },
            _ => options,
        };
        var ct = TestContext.Current.CancellationToken;
        if (scenario == "existingReport")
        {
            await File.WriteAllTextAsync(Path.Combine(directory.Path, patch + ".json"), "{}", ct);
        }

        // Act
        var code = await NuGetUpgradeTrial.Run(options, output, error, ct);

        // Assert
        code.ShouldBe(1);
        error.ToString().ShouldContain(diagnostic, StringComparison.Ordinal);
        File.Exists(Path.Combine(directory.Path, patch)).ShouldBeFalse();
    }
}
