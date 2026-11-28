namespace SharedKernel.Versioning.Tests;

/// <summary>Exercises package-time symbol inventory and the original explicit-subset verifier.</summary>
[Trait(TestTraitNames.CapabilityName, TestTraits.VersioningCapability)]
public static class CandidateSymbolInventoryTests
{
    [Fact]
    public static async Task Candidate_creation_records_the_packed_symbols_before_a_later_omission()
    {
        // Arrange
        using var archives = new CandidateSourceLinkTestFeed();
        using var candidate = new CandidateSymbolPackFixture();
        var ct = TestContext.Current.CancellationToken;
        await archives.Write(CandidateSourceLinkTestFeed.CandidateRevision, mismatchedAssembly: false, ct, includeOtherPackage: true);
        var command = await candidate.CreateCommand(archives.DirectoryPath, ct);
        var manifest = Path.Combine(candidate.PackageDirectory, "candidate.json");
        var inventory = Path.Combine(candidate.PackageDirectory, "symbols.sha256");
        var removed = CandidateSourceLinkTestFeed.SymbolFileName;
        var retained = $"{CandidateSourceLinkTestFeed.OtherPackageId}.{CandidateSourceLinkTestFeed.PackageVersion}.snupkg";
        var removedHash = await CandidateManifest.HashPackage(Path.Combine(archives.DirectoryPath, removed), ct);
        var retainedHash = await CandidateManifest.HashPackage(Path.Combine(archives.DirectoryPath, retained), ct);
        var captured = $"{retainedHash}  ./{retained}\n{removedHash}  ./{removed}\n";
        var fullReport = Path.Combine(candidate.Root, "full-provenance.json");
        var missingReport = Path.Combine(candidate.Root, "missing-provenance.json");
        var subsetReport = Path.Combine(candidate.Root, "subset-provenance.json");
        var subsetPath = Path.Combine(candidate.Root, "subset.sha256");
        string[] verify = ["verify-candidate-source-link", "--candidate", manifest,
            "--source-url-pattern", CandidateSourceLinkTestFeed.SourcePattern];

        // Act
        var created = await VersioningCommandLineRunner.Invoke(command,
            ["create-candidate", "--repo-root", candidate.Root, "--version", CandidateSourceLinkTestFeed.PackageVersion, "--record-symbols"], ct);
        created.ExitCode.ShouldBe(0, created.Error);
        var full = await VersioningCommandLineRunner.Invoke(command, [.. verify, "--symbols", inventory, "--output", fullReport], ct);
        File.Delete(Path.Combine(candidate.PackageDirectory, removed));
        var missing = await VersioningCommandLineRunner.Invoke(command, [.. verify, "--symbols", inventory, "--output", missingReport], ct);
        await File.WriteAllTextAsync(subsetPath, $"{retainedHash}  ./{retained}\n", ct);
        var subset = await VersioningCommandLineRunner.Invoke(command, [.. verify, "--symbols", subsetPath, "--output", subsetReport], ct);

        // Assert
        full.ExitCode.ShouldBe(0, full.Error);
        missing.ExitCode.ShouldBe(2, missing.Error);
        missing.Error.ShouldContain(removed, StringComparison.Ordinal);
        File.Exists(missingReport).ShouldBeFalse();
        subset.ExitCode.ShouldBe(0, subset.Error);
        var remainingInventory = await File.ReadAllTextAsync(inventory, ct);
        remainingInventory.ShouldBe(captured);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public static async Task Candidate_creation_requires_a_nonempty_inventory_only_when_requested(bool record)
    {
        // Arrange
        using var archives = new CandidateSourceLinkTestFeed();
        using var candidate = new CandidateSymbolPackFixture();
        var ct = TestContext.Current.CancellationToken;
        await archives.Write(CandidateSourceLinkTestFeed.CandidateRevision, mismatchedAssembly: false, ct);
        File.Delete(Path.Combine(archives.DirectoryPath, CandidateSourceLinkTestFeed.SymbolFileName));
        var command = await candidate.CreateCommand(archives.DirectoryPath, ct);
        string[] args = ["create-candidate", "--repo-root", candidate.Root, "--version", CandidateSourceLinkTestFeed.PackageVersion];
        if (record)
        {
            args = [.. args, "--record-symbols"];
        }

        // Act
        var result = await VersioningCommandLineRunner.Invoke(command, args, ct);

        // Assert
        result.ExitCode.ShouldBe(record ? 2 : 0, result.Error);
        File.Exists(Path.Combine(candidate.PackageDirectory, "candidate.json")).ShouldBe(!record);
        File.Exists(Path.Combine(candidate.PackageDirectory, "symbols.sha256")).ShouldBeFalse();
        if (record)
        {
            result.Error.ShouldContain("The candidate pack produced no symbol archives.", StringComparison.Ordinal);
        }
    }
}
