namespace PandaBear.Versioning.Tests;

/// <summary>
/// Tests the consumer projects that package consumption generates from the samples to restore, build, and run the packages.
/// </summary>
[Trait(SharedKernelTestTraitNames.CapabilityName, TestTraits.VersioningCapability)]
public static class PackageConsumptionProjectTests
{
    [Fact]
    public static async Task Consumer_projects_audit_every_dependency_at_every_severity_as_errors()
    {
        // Arrange
        const string ResultPrefix = "Package consumption checks passed. Evidence workspace: ";
        using var temporary = new TemporaryReleasePrepDirectory();
        var manifest = temporary.WriteCandidate();
        await temporary.WriteConsumptionSamples("Consumed sample", TestContext.Current.CancellationToken);

        // Act
        var result = await VersioningCommandLineRunner.Invoke(
            ["consume-candidate", "--candidate", manifest, "--repo-root", temporary.Root],
            TestContext.Current.CancellationToken);
        using var evidence = new EvidenceWorkspaces(result);

        // Assert
        var succeeded = result.ExitCode == 0;
        succeeded.ShouldBeTrue($"consume-candidate exited with {result.ExitCode}. Standard error:{Environment.NewLine}{result.Error}");
        var workspace = result.Output[ResultPrefix.Length..].Trim();
        foreach (var sample in new[] { "BasicResults.Sample", "Mediator.Sample", "HttpRequests.Sample" })
        {
            var project = await File.ReadAllTextAsync(Path.Combine(workspace, sample, sample + ".csproj"), TestContext.Current.CancellationToken);
            project.ShouldContain("<TreatWarningsAsErrors>true</TreatWarningsAsErrors>", StringComparison.Ordinal, sample);
            project.ShouldContain("<NuGetAudit>true</NuGetAudit>", StringComparison.Ordinal, sample);
            project.ShouldContain("<NuGetAuditMode>all</NuGetAuditMode>", StringComparison.Ordinal, sample);
            project.ShouldNotContain("NuGetAuditLevel", StringComparison.Ordinal, $"{sample} keeps NuGet's default level, low, so advisories of every severity fail the consumer.");
        }
    }
}
