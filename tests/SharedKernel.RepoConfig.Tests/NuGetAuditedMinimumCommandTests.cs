using System.Text.Json;

namespace PandaBear.RepoConfig.Tests;

/// <summary>Exercises ordered upgrade planning under real locked restore and package source mapping.</summary>
[Trait(TestTraitNames.CategoryName, TestTraits.CommandLineCategory)]
public sealed class NuGetAuditedMinimumCommandTests
{
    [Theory]
    [InlineData("single", "1.2.0", 1)]
    [InlineData("tied", "1.1.0", 1)]
    [InlineData("coupled", "1.1.0", 2)]
    public async Task Trial_finds_the_smallest_safe_mapped_owner_bundle(string scenario, string expectedVersion, int expectedOwners)
    {
        // Arrange
        await using var audit = new NuGetTrialAuditFeed(NuGetTrialRepository.Core, "(,2.0.0)");
        using var fixture = new NuGetTrialRepository();
        await fixture.Initialize(audit: audit, minimum: scenario);
        var props = Path.Combine(fixture.Root, "Directory.Packages.props");
        var locked = Path.Combine(fixture.Root, NuGetTrialRepository.LockedPath);
        var originalProps = await File.ReadAllTextAsync(props, TestContext.Current.CancellationToken);
        var originalLock = await File.ReadAllTextAsync(locked, TestContext.Current.CancellationToken);
        const string patch = ".local/minimum.patch";
        string[] coordinates = scenario == "coupled" ? ["--coordinate", NuGetTrialRepository.Partner] : [];

        // Act
        var result = await fixture.RunAudit(patch, coordinates);

        // Assert
        var reportText = await File.ReadAllTextAsync(Path.Combine(fixture.Root, patch + ".json"), TestContext.Current.CancellationToken);
        result.ExitCode.ShouldBe(0, result.StandardError + reportText);
        using var report = JsonDocument.Parse(reportText);
        var minimum = report.RootElement.GetProperty("minimum");
        minimum.GetProperty("status").GetString().ShouldBe("proven");
        var updates = minimum.GetProperty("selectedUpdates").EnumerateArray().Select(item => item.GetString()).ToArray();
        updates.Length.ShouldBe(expectedOwners);
        updates[0].ShouldBe(NuGetTrialRepository.Driver + "@" + expectedVersion);
        if (scenario == "coupled")
        {
            updates[1].ShouldBe(NuGetTrialRepository.Partner + "@" + expectedVersion);
        }

        var steps = report.RootElement.GetProperty("steps").EnumerateArray().ToArray();
        var final = steps.ShouldHaveSingleItem(step => step.GetProperty("name").GetString() == "candidateLockedRestore");
        final.GetProperty("exitCode").GetInt32().ShouldBe(0);
        audit.UnmappedRequests.ShouldBe(0);
        var sourceProps = await File.ReadAllTextAsync(props, TestContext.Current.CancellationToken);
        var sourceLock = await File.ReadAllTextAsync(locked, TestContext.Current.CancellationToken);
        sourceProps.ShouldBe(originalProps);
        sourceLock.ShouldBe(originalLock);
        File.Exists(Path.Combine(fixture.Root, "src/exempt/packages.lock.json")).ShouldBeFalse();
        var git = await fixture.Git("apply", "--check", patch);
        git.ExitCode.ShouldBe(0);
    }

    [Fact]
    public async Task Trial_keeps_the_minimum_unproven_when_an_earlier_candidate_loses_a_source()
    {
        // Arrange
        await using var audit = new NuGetTrialAuditFeed(NuGetTrialRepository.Core, "(,2.0.0)");
        using var fixture = new NuGetTrialRepository();
        await fixture.Initialize(audit: audit, minimum: "toleratedSourceWarning");
        const string patch = ".local/unavailable-earlier.patch";

        // Act
        var result = await fixture.RunAudit(patch);

        // Assert
        var reportText = await File.ReadAllTextAsync(Path.Combine(fixture.Root, patch + ".json"), TestContext.Current.CancellationToken);
        using var report = JsonDocument.Parse(reportText);
        var steps = report.RootElement.GetProperty("steps").EnumerateArray().ToArray();
        var failed = steps.ShouldHaveSingleItem(step =>
            step.GetProperty("name").GetString() == "candidateRestore" && step.GetProperty("exitCode").GetInt32() != 0);
        var codes = failed.GetProperty("codes").EnumerateArray().Select(item => item.GetString()).ToArray();
        codes.ShouldBe(["NU1801", "NU1903"]);
        var errors = failed.GetProperty("errors").EnumerateArray().Select(item => item.GetString());
        errors.ShouldBe(["NU1903"]);
        result.ExitCode.ShouldBe(1, reportText);
        var minimum = report.RootElement.GetProperty("minimum");
        minimum.GetProperty("status").GetString().ShouldBe("candidateUnavailable");
        minimum.GetProperty("attempts").GetInt32().ShouldBe(1);
        minimum.GetProperty("selectedUpdates").GetArrayLength().ShouldBe(0);
        File.Exists(Path.Combine(fixture.Root, patch)).ShouldBeFalse();
    }

    [Theory]
    [InlineData("unavailable", "partial")]
    [InlineData("ambiguous", "ambiguousSources")]
    public async Task Trial_withholds_a_minimum_when_source_evidence_is_indeterminate(string scenario, string catalogStatus)
    {
        // Arrange
        await using var audit = new NuGetTrialAuditFeed(NuGetTrialRepository.Core, "(,2.0.0)");
        using var fixture = new NuGetTrialRepository();
        await fixture.Initialize(audit: audit, minimum: scenario);
        const string patch = ".local/unknown.patch";

        // Act
        string[] sources = scenario == "ambiguous" ? ["--copy-local-source", ".local/unavailable"] : [];
        var result = await fixture.RunAudit(patch, sources);

        // Assert
        result.ExitCode.ShouldBe(1, result.StandardError);
        var reportText = await File.ReadAllTextAsync(Path.Combine(fixture.Root, patch + ".json"), TestContext.Current.CancellationToken);
        using var report = JsonDocument.Parse(reportText);
        report.RootElement.GetProperty("status").GetString().ShouldBe("minimumUnproven");
        var minimum = report.RootElement.GetProperty("minimum");
        minimum.GetProperty("status").GetString().ShouldBe("metadataUnavailable");
        var catalog = minimum.GetProperty("catalog").EnumerateArray().ShouldHaveSingleItem();
        catalog.GetProperty("status").GetString().ShouldBe(catalogStatus);
        File.Exists(Path.Combine(fixture.Root, patch)).ShouldBeFalse();
        audit.UnmappedRequests.ShouldBe(0);
    }
}
