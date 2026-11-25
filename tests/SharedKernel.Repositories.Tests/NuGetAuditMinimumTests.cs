using System.Text.Json;

namespace SharedKernel.Repositories.Tests;

/// <summary>Verifies the ordering and completeness rules of the automatic audit search.</summary>
public sealed class NuGetAuditMinimumTests
{
    [Fact]
    public void Candidates_order_by_cardinality_then_owner_and_version()
    {
        // Arrange
        NuGetVersionSet[] catalogs =
        [
            new("Zulu", "complete", ["2.0.0", "2.1.0"]),
            new("Empty", "complete", []),
            new("Alpha", "complete", ["1.2.0", "1.10.0"]),
        ];

        // Act
        var candidates = NuGetAuditMinimum.Candidates(catalogs, includeUnchanged: true)
            .Select(bundle => string.Join(" ", bundle.Select(update => update.Key + "@" + update.Value))).ToArray();

        // Assert
        candidates.ShouldBe(
        [
            "", "Alpha@1.2.0", "Alpha@1.10.0", "Zulu@2.0.0", "Zulu@2.1.0",
            "Alpha@1.2.0 Zulu@2.0.0", "Alpha@1.2.0 Zulu@2.1.0",
            "Alpha@1.10.0 Zulu@2.0.0", "Alpha@1.10.0 Zulu@2.1.0",
        ]);
    }

    [Fact]
    public void Owners_require_each_advisory_to_match_its_evaluated_project()
    {
        // Arrange
        using var asset = JsonDocument.Parse(NuGetRestoreGraphTestAssets.Unpinned);
        var graph = NuGetRestoreGraph.Read(asset.RootElement);
        const string project = "src/Provider.csproj";
        var baseline = new NuGetRestoreGraphSource("revision", false, "11.0.100", "Example.slnx",
            new(StringComparer.Ordinal) { [project] = new(false, graph, null) }, null);
        var advisory = new NuGetTrialAdvisory("NU1903", "Provider.Core", "2.0.0", "https://example.invalid/advisory", project);

        // Act
        var owners = NuGetAuditMinimum.Owners(baseline, [advisory], ["Companion"]);
        var unrelated = NuGetAuditMinimum.Owners(baseline, [advisory with { Project = "Another.csproj" }], []);
        var missingSource = NuGetAuditMinimum.Requests(baseline, ["Provider"]);

        // Assert
        owners.ShouldBe(["Companion", "Provider"]);
        unrelated.ShouldBeNull();
        missingSource.ShouldBeNull();
    }

    [Fact]
    public void Selected_version_must_match_every_target_that_directly_uses_the_owner()
    {
        // Arrange
        using var before = JsonDocument.Parse(NuGetRestoreGraphTestAssets.Unpinned);
        using var after = JsonDocument.Parse(NuGetRestoreGraphTestAssets.Upgraded);
        const string project = "src/Provider.csproj";
        var baseline = new NuGetRestoreGraphSource("revision", false, "11.0.100", "Example.slnx",
            new(StringComparer.Ordinal) { [project] = new(false, NuGetRestoreGraph.Read(before.RootElement), null) }, null);
        var candidate = baseline with
        {
            Projects = new(StringComparer.Ordinal) { [project] = new(false, NuGetRestoreGraph.Read(after.RootElement), null) },
        };

        // Act
        var exact = NuGetAuditMinimum.Matches(baseline, candidate, [new("Provider", "3.0.0")]);
        var floated = NuGetAuditMinimum.Matches(baseline, candidate, [new("Provider", "2.1.0")]);
        var missing = NuGetAuditMinimum.Matches(baseline, candidate with { Projects = new(StringComparer.Ordinal) }, [new("Provider", "3.0.0")]);

        // Assert
        exact.ShouldBeTrue();
        floated.ShouldBeFalse();
        missing.ShouldBeFalse();
    }
}
