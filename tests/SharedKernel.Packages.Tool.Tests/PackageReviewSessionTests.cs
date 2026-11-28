namespace SharedKernel.Packages.Tool.Tests;

/// <summary>Verifies unreliable or incomplete observations are visible in interactive review.</summary>
public sealed class PackageReviewSessionTests
{
    [Fact]
    public async Task A_missing_tool_is_reported_for_both_observations_without_running_anything()
    {
        // Arrange
        using var repository = PackagesToolHarness.CreateRepository(
            """{"version":1,"packages":{"cargo":["bat"]},"minimumReleaseAgeDays":7}""");
        var runner = ScriptedProcessRunner.ByFileName([]);

        // Act
        var review = await ReviewToolHarness.Observe(repository.Path, runner, includeUpdates: true, PackagesToolHarness.ResolveWithoutCargo);
        var details = review.DetailsFor(PackageManagerKind.Cargo);

        // Assert
        details.ShouldContain("Installed state unavailable. Missing on PATH: cargo.");
        details.ShouldContain("Update state unavailable. Missing on PATH: cargo.");
        details.ShouldContain("bat: installed state unknown");
        review.ExitCode.ShouldBe(2);
        runner.Runs.ShouldBeEmpty();
    }

    [Fact]
    public async Task An_update_read_failure_preserves_the_known_installed_state()
    {
        // Arrange
        using var repository = PackagesToolHarness.CreateRepository(
            """{"version":1,"packages":{"cargo":["bat"]},"minimumReleaseAgeDays":7}""");
        var runner = ScriptedProcessRunner.InOrder(new(0, UpdatesToolHarness.CargoList), new(3, string.Empty, "cargo unavailable"));

        // Act
        var review = await ReviewToolHarness.Observe(repository.Path, runner, includeUpdates: true);
        var details = review.DetailsFor(PackageManagerKind.Cargo);

        // Assert
        details.ShouldContain("bat: installed as declared");
        details.ShouldContain(static line => line.Contains("cargo unavailable", StringComparison.Ordinal));
        review.ExitCode.ShouldBe(2);
    }

    [Fact]
    public async Task A_missing_installation_and_unconfigured_manager_show_distinct_observations()
    {
        // Arrange
        using var repository = PackagesToolHarness.CreateRepository(
            """{"version":1,"packages":{"cargo":["missing"]},"minimumReleaseAgeDays":7}""");
        var runner = UpdatesToolHarness.Runner();

        // Act
        var review = await ReviewToolHarness.Observe(repository.Path, runner, includeUpdates: true);
        var declared = review.DetailsFor(PackageManagerKind.Cargo);
        var unconfigured = review.DetailsFor(PackageManagerKind.Npm);

        // Assert
        declared.ShouldContain("Install missing");
        declared.ShouldContain("No update decisions for the declared packages.");
        unconfigured.ShouldContain("No updates were checked.");
        review.ExitCode.ShouldBe(1);
    }
}
