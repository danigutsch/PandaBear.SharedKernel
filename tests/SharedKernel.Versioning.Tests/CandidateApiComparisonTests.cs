using System.Text.Json.Nodes;
using PandaBear.Processes;
using PandaBear.Versioning.Tool;

namespace PandaBear.Versioning.Tests;

/// <summary>Verifies evidence generated when comparing two packaged candidates.</summary>
[Trait(SharedKernelTestTraitNames.CapabilityName, TestTraits.VersioningCapability)]
public static class CandidateApiComparisonTests
{
    private const string BaselineRevision = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    private const string CandidateRevision = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
    private const string BaselineVersion = "1.0.0-alpha.1";
    private const string CandidateVersion = "1.0.0-alpha.2";
    private const string Probe = "PandaBear.SharedKernel.Probe";

    [Fact]
    public static async Task A_changed_baseline_archive_is_rejected_before_API_comparison()
    {
        // Arrange
        using var baseline = new TemporaryReleasePrepDirectory();
        using var candidate = new TemporaryReleasePrepDirectory();
        var ct = TestContext.Current.CancellationToken;
        var baselineManifest = baseline.WriteCandidate();
        var candidateManifest = candidate.WriteCandidate();
        var baselinePackage = Directory.EnumerateFiles(baseline.PackageDirectory, "*.nupkg").ShouldHaveSingleItem();
        await File.AppendAllTextAsync(baselinePackage, "changed archive", ct);
        var report = Path.Combine(candidate.Root, "api-comparison.json");

        // Act
        var result = await VersioningCommandLineRunner.Invoke([
            "compare-candidates", "--baseline", baselineManifest, "--candidate", candidateManifest,
            "--apicompat", Path.Combine(candidate.Root, "apicompat"), "--output", report,
        ], ct);

        // Assert
        result.ExitCode.ShouldBe(2);
        result.Error.ShouldContain("Candidate package changed", StringComparison.Ordinal);
        File.Exists(report).ShouldBeFalse();
    }

    [Fact]
    public static async Task An_additive_candidate_comparison_records_both_verified_identities_and_scope()
    {
        // Arrange
        using var baseline = new CandidateComparisonTestFeed();
        using var candidate = new CandidateComparisonTestFeed();
        var ct = TestContext.Current.CancellationToken;
        var baselineManifest = await baseline.Write(BaselineVersion, BaselineRevision, ct,
            (Probe, true), ("PandaBear.SharedKernel.Tool", false));
        var candidateManifest = await candidate.Write(CandidateVersion, CandidateRevision, ct,
            (Probe, true), ("PandaBear.SharedKernel.Tool", false), ("PandaBear.SharedKernel.Added", true));
        var runner = new ComparisonApiCompatRunner(new ProcessOutput(0, "APICompat ran successfully without finding any breaking changes."));
        var command = VersioningCommandLine.Create(TextReader.Null, TimeProvider.System, runner, PathExecutableResolver.Default);
        var report = Path.Combine(candidate.DirectoryPath, "api-comparison.json");

        // Act
        var result = await VersioningCommandLineRunner.Invoke(command,
            ["compare-candidates", "--baseline", baselineManifest, "--candidate", candidateManifest, "--apicompat", "apicompat", "--output", report], ct);
        var evidence = JsonNode.Parse(await File.ReadAllTextAsync(report, ct)).ShouldNotBeNull();

        // Assert
        result.ExitCode.ShouldBe(0);
        evidence["status"].ShouldNotBeNull().GetValue<string>().ShouldBe("passed");
        evidence["apiCompatVersion"].ShouldNotBeNull().GetValue<string>().ShouldBe("11.0.100+test");
        var earlier = evidence["baseline"].ShouldNotBeNull();
        earlier["libraryRevision"].ShouldNotBeNull().GetValue<string>().ShouldBe(BaselineRevision);
        earlier["manifestSha256"].ShouldNotBeNull().GetValue<string>().ShouldBe(await CandidateManifest.HashPackage(baselineManifest, ct));
        var current = evidence["candidate"].ShouldNotBeNull();
        current["packageVersion"].ShouldNotBeNull().GetValue<string>().ShouldBe(CandidateVersion);
        current["manifestSha256"].ShouldNotBeNull().GetValue<string>().ShouldBe(await CandidateManifest.HashPackage(candidateManifest, ct));
        var compared = runner.Comparisons.ShouldHaveSingleItem();
        compared.CandidateHash.ShouldBe(current["packageSha256"]![Path.GetFileName(compared.Candidate)]!.GetValue<string>());
        compared.BaselineHash.ShouldBe(earlier["packageSha256"]![Path.GetFileName(compared.Baseline)]!.GetValue<string>());
        compared.Candidate.ShouldNotContain(candidate.DirectoryPath, StringComparison.Ordinal);
        compared.Baseline.ShouldNotContain(baseline.DirectoryPath, StringComparison.Ordinal);
        evidence["packages"]!.AsArray().Select(package => package!["status"]!.GetValue<string>())
            .ShouldBe(["added", "passed", "no-baseline-assembly"]);
    }

    [Fact]
    public static async Task A_member_removal_is_reported_as_a_completed_comparison_with_differences()
    {
        // Arrange
        using var baseline = new CandidateComparisonTestFeed();
        using var candidate = new CandidateComparisonTestFeed();
        var ct = TestContext.Current.CancellationToken;
        var baselineManifest = await baseline.Write(BaselineVersion, BaselineRevision, ct, (Probe, true));
        var candidateManifest = await candidate.Write(CandidateVersion, CandidateRevision, ct, (Probe, true));
        const string diagnostic = "CP0002: Member 'Probe.Removed' exists on the baseline.";
        var runner = new ComparisonApiCompatRunner(new ProcessOutput(1, diagnostic));
        var command = VersioningCommandLine.Create(TextReader.Null, TimeProvider.System, runner, PathExecutableResolver.Default);
        var report = Path.Combine(candidate.DirectoryPath, "api-comparison.json");

        // Act
        var result = await VersioningCommandLineRunner.Invoke(command,
            ["compare-candidates", "--baseline", baselineManifest, "--candidate", candidateManifest, "--apicompat", "apicompat", "--output", report], ct);
        var evidence = JsonNode.Parse(await File.ReadAllTextAsync(report, ct)).ShouldNotBeNull();

        // Assert
        result.ExitCode.ShouldBe(3);
        result.Error.ShouldBe(string.Empty);
        evidence["status"].ShouldNotBeNull().GetValue<string>().ShouldBe("differences");
        var package = evidence["packages"]!.AsArray().ShouldHaveSingleItem().ShouldNotBeNull();
        package["diagnostics"]!.AsArray().ShouldHaveSingleItem().ShouldNotBeNull().GetValue<string>().ShouldBe(diagnostic);
    }

    [Theory]
    [InlineData(1, "Unrecognized argument.")]
    [InlineData(2, "CP0002: Failed process left a diagnostic.")]
    [InlineData(0, "PKV0001: Reported a difference despite success.")]
    public static async Task An_unexpected_tool_result_fails_without_publishing_a_report(int exitCode, string text)
    {
        // Arrange
        using var baseline = new CandidateComparisonTestFeed();
        using var candidate = new CandidateComparisonTestFeed();
        var ct = TestContext.Current.CancellationToken;
        var baselineManifest = await baseline.Write(BaselineVersion, BaselineRevision, ct, (Probe, true));
        var candidateManifest = await candidate.Write(CandidateVersion, CandidateRevision, ct, (Probe, true));
        var runner = new ComparisonApiCompatRunner(new ProcessOutput(exitCode, string.Empty, text));
        var command = VersioningCommandLine.Create(TextReader.Null, TimeProvider.System, runner, PathExecutableResolver.Default);
        var report = Path.Combine(candidate.DirectoryPath, "api-comparison.json");

        // Act
        var result = await VersioningCommandLineRunner.Invoke(command,
            ["compare-candidates", "--baseline", baselineManifest, "--candidate", candidateManifest, "--apicompat", "apicompat", "--output", report], ct);

        // Assert
        result.ExitCode.ShouldBe(2);
        result.Error.ShouldContain("ApiCompat could not complete the comparison", StringComparison.Ordinal);
        result.Error.ShouldContain(text, StringComparison.Ordinal);
        File.Exists(report).ShouldBeFalse();
    }

    [Fact]
    public static async Task A_missing_baseline_archive_fails_before_starting_the_tool()
    {
        // Arrange
        using var baseline = new CandidateComparisonTestFeed();
        using var candidate = new CandidateComparisonTestFeed();
        var ct = TestContext.Current.CancellationToken;
        var baselineManifest = await baseline.Write(BaselineVersion, BaselineRevision, ct, (Probe, true));
        var candidateManifest = await candidate.Write(CandidateVersion, CandidateRevision, ct, (Probe, true));
        File.Delete(Directory.EnumerateFiles(baseline.DirectoryPath, "*.nupkg").ShouldHaveSingleItem());
        var report = Path.Combine(candidate.DirectoryPath, "api-comparison.json");

        // Act
        var result = await VersioningCommandLineRunner.Invoke(
            ["compare-candidates", "--baseline", baselineManifest, "--candidate", candidateManifest, "--apicompat", "apicompat", "--output", report], ct);

        // Assert
        result.ExitCode.ShouldBe(2);
        result.Error.ShouldContain(Probe, StringComparison.Ordinal);
        File.Exists(report).ShouldBeFalse();
    }

    [Fact]
    public static async Task A_removed_package_is_recorded_without_invoking_an_assembly_comparison()
    {
        // Arrange
        using var baseline = new CandidateComparisonTestFeed();
        using var candidate = new CandidateComparisonTestFeed();
        var ct = TestContext.Current.CancellationToken;
        const string replacement = "PandaBear.SharedKernel.Replacement";
        var baselineManifest = await baseline.Write(BaselineVersion, BaselineRevision, ct, (Probe, true));
        var candidateManifest = await candidate.Write(CandidateVersion, CandidateRevision, ct, (replacement, true));
        var runner = new ComparisonApiCompatRunner(new ProcessOutput(0, string.Empty));
        var command = VersioningCommandLine.Create(TextReader.Null, TimeProvider.System, runner, PathExecutableResolver.Default);
        var report = Path.Combine(candidate.DirectoryPath, "api-comparison.json");

        // Act
        var result = await VersioningCommandLineRunner.Invoke(command,
            ["compare-candidates", "--baseline", baselineManifest, "--candidate", candidateManifest, "--apicompat", "apicompat", "--output", report], ct);
        var evidence = JsonNode.Parse(await File.ReadAllTextAsync(report, ct)).ShouldNotBeNull();

        // Assert
        result.ExitCode.ShouldBe(3);
        runner.Comparisons.ShouldBeEmpty();
        var packages = evidence["packages"].ShouldNotBeNull().AsArray();
        var removed = packages[0].ShouldNotBeNull();
        removed["packageId"]!.GetValue<string>().ShouldBe(Probe);
        removed["status"]!.GetValue<string>().ShouldBe("removed");
        removed["candidatePackage"].ShouldBeNull();
        packages[1]!["status"]!.GetValue<string>().ShouldBe("added");
    }

    [Theory]
    [InlineData("{}", "Candidate package hashes are unavailable")]
    [InlineData("[", "Candidate evidence cannot be read")]
    public static async Task Invalid_candidate_manifests_fail_before_starting_the_tool(string content, string error)
    {
        // Arrange
        using var baseline = new CandidateComparisonTestFeed();
        using var candidate = new CandidateComparisonTestFeed();
        var ct = TestContext.Current.CancellationToken;
        var baselineManifest = await baseline.Write(BaselineVersion, BaselineRevision, ct, (Probe, true));
        var candidateManifest = await candidate.Write(CandidateVersion, CandidateRevision, ct, (Probe, true));
        await File.WriteAllTextAsync(candidateManifest, content, ct);
        var report = Path.Combine(candidate.DirectoryPath, "api-comparison.json");

        // Act
        var result = await VersioningCommandLineRunner.Invoke(
            ["compare-candidates", "--baseline", baselineManifest, "--candidate", candidateManifest, "--apicompat", "apicompat", "--output", report], ct);

        // Assert
        result.ExitCode.ShouldBe(2);
        result.Error.ShouldContain(error, StringComparison.Ordinal);
        File.Exists(report).ShouldBeFalse();
    }

    [Fact]
    public static async Task An_existing_report_is_preserved()
    {
        // Arrange
        using var baseline = new CandidateComparisonTestFeed();
        using var candidate = new CandidateComparisonTestFeed();
        var ct = TestContext.Current.CancellationToken;
        var baselineManifest = await baseline.Write(BaselineVersion, BaselineRevision, ct, (Probe, true));
        var candidateManifest = await candidate.Write(CandidateVersion, CandidateRevision, ct, (Probe, true));
        var report = Path.Combine(candidate.DirectoryPath, "api-comparison.json");
        const string original = "existing report";
        await File.WriteAllTextAsync(report, original, ct);

        // Act
        var result = await VersioningCommandLineRunner.Invoke(
            ["compare-candidates", "--baseline", baselineManifest, "--candidate", candidateManifest, "--apicompat", "apicompat", "--output", report], ct);

        // Assert
        result.ExitCode.ShouldBe(2);
        result.Error.ShouldContain("already exists", StringComparison.Ordinal);
        var current = await File.ReadAllTextAsync(report, ct);
        current.ShouldBe(original);
    }
}
