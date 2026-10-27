using System.Text.Json.Nodes;
using PandaBear.Tooling;

namespace PandaBear.Versioning.Tests;

/// <summary>
/// Tests the merge-evidence command that merges platform evidence files into a candidate manifest.
/// </summary>
public static class CandidateEvidenceMergeTests
{
    [Fact]
    public static async Task Passed_platform_evidence_is_merged_into_the_candidate_and_other_fields_are_kept()
    {
        // Arrange
        using var temporary = new TemporaryReleasePrepDirectory();
        var candidate = CandidateEvidenceFiles.Write(temporary.Root, "candidate.json");
        var linux = CandidateEvidenceFiles.Write(temporary.Root, "linux.json", ("linuxPackageConsumption", "passed"), ("linuxNativeAot", "passed"));
        var windows = CandidateEvidenceFiles.Write(temporary.Root, "windows.json", ("windowsPackageConsumption", "passed"), ("windowsNativeAot", "passed"));
        var output = Path.Combine(temporary.Root, "merged.json");

        // Act
        var result = await VersioningCommandLineRunner.Invoke(
            ["merge-evidence", "--candidate", candidate, "--evidence", linux, windows, "--output", output],
            TestContext.Current.CancellationToken);

        // Assert
        result.ExitCode.ShouldBe(0, result.Error);
        var merged = JsonNode.Parse(await File.ReadAllTextAsync(output, TestContext.Current.CancellationToken))!.AsObject();
        merged["linuxPackageConsumption"]!.GetValue<string>().ShouldBe("passed");
        merged["linuxNativeAot"]!.GetValue<string>().ShouldBe("passed");
        merged["windowsPackageConsumption"]!.GetValue<string>().ShouldBe("passed");
        merged["windowsNativeAot"]!.GetValue<string>().ShouldBe("passed");
        merged["consumerCompatibility"]!.GetValue<string>().ShouldBe("pending");
        merged["packageSha256"]!.ToJsonString().ShouldBe(JsonNode.Parse(await File.ReadAllTextAsync(candidate, TestContext.Current.CancellationToken))!["packageSha256"]!.ToJsonString());
    }

    [Fact]
    public static async Task Evidence_for_another_revision_is_rejected_and_nothing_is_written()
    {
        // Arrange
        using var temporary = new TemporaryReleasePrepDirectory();
        var candidate = CandidateEvidenceFiles.Write(temporary.Root, "candidate.json");
        var other = CandidateEvidenceFiles.Write(temporary.Root, "other.json", ("linuxPackageConsumption", "passed"));
        var json = JsonNode.Parse(await File.ReadAllTextAsync(other, TestContext.Current.CancellationToken))!.AsObject();
        json["libraryRevision"] = "0000000000000000000000000000000000000000";
        CandidateManifest.Write(other, json);
        var output = Path.Combine(temporary.Root, "merged.json");

        // Act
        var result = await VersioningCommandLineRunner.Invoke(
            ["merge-evidence", "--candidate", candidate, "--evidence", other, "--output", output],
            TestContext.Current.CancellationToken);

        // Assert
        result.ExitCode.ShouldBe(2);
        result.Error.ShouldContain("libraryRevision", StringComparison.Ordinal);
        File.Exists(output).ShouldBeFalse();
    }

    [Fact]
    public static async Task Failed_platform_evidence_is_rejected_and_nothing_is_written()
    {
        // Arrange
        using var temporary = new TemporaryReleasePrepDirectory();
        var candidate = CandidateEvidenceFiles.Write(temporary.Root, "candidate.json");
        var failed = CandidateEvidenceFiles.Write(temporary.Root, "failed.json", ("linuxNativeAot", "failed"));
        var output = Path.Combine(temporary.Root, "merged.json");

        // Act
        var result = await VersioningCommandLineRunner.Invoke(
            ["merge-evidence", "--candidate", candidate, "--evidence", failed, "--output", output],
            TestContext.Current.CancellationToken);

        // Assert
        result.ExitCode.ShouldBe(2);
        result.Error.ShouldContain("linuxNativeAot", StringComparison.Ordinal);
        File.Exists(output).ShouldBeFalse();
    }
}
