using System.Diagnostics;
using System.Text.Json.Nodes;
using SharedKernel.Testing.Snapshots;

namespace SharedKernel.Testing.Tests;

/// <summary>Verifies the versioned test-attempt format and the fixture allowlist.</summary>
public sealed class ReproductionManifestTests
{
    private const string Revision = "a1b2c3d4e5f6a1b2c3d4e5f6a1b2c3d4e5f6a1b2";

    [Theory]
    [InlineData("venue.editor.preview", "browser", "valid", 7)]
    [InlineData("venue.index.refresh", "worker", "updated", null)]
    public async Task Approved_metadata_round_trips_without_extra_fields(string scenario, string resource, string state, int? seed)
    {
        // Arrange
        using var directory = new TemporaryDirectory();
        var ct = TestContext.Current.CancellationToken;
        var outcome = new ReproductionOutcome("completed", "confirmed", new Dictionary<string, string>(StringComparer.Ordinal) { [resource] = state });
        var specification = new ReproductionSpecification(scenario, Revision, [outcome], seed);
        var trace = ActivityTraceId.CreateRandom();
        var attempt = new ReproductionManifest(Revision, scenario, Guid.NewGuid(), [trace], outcome, DateTimeOffset.UnixEpoch, 25, seed);

        // Act
        var path = await ReproductionManifestCodec.Write(directory.Path, specification, attempt, ct);
        var actual = await ReproductionManifestCodec.Read(path, specification, ct);
        var text = await File.ReadAllTextAsync(path, ct);

        // Assert
        Path.GetFileName(path).ShouldBe($"{attempt.AttemptId:N}.json");
        actual.AttemptId.ShouldBe(attempt.AttemptId);
        actual.TraceIds.ShouldBe([trace]);
        actual.Outcome.ResourceStates.ShouldBe(outcome.ResourceStates);
        actual.Seed.ShouldBe(seed);
        JsonNode.Parse(text).ShouldNotBeNull().AsObject().Count.ShouldBe(seed is null ? 10 : 11);
    }

    [Theory]
    [InlineData("browserState")]
    [InlineData("scenario")]
    [InlineData("outcomeCode")]
    public async Task Contaminated_manifests_are_refused_without_echoing_the_value(string field)
    {
        // Arrange
        const string contamination = "fictional confidential value";
        using var directory = new TemporaryDirectory();
        var ct = TestContext.Current.CancellationToken;
        var (specification, manifest) = ReproductionManifestTestData.Create();
        var path = await ReproductionManifestCodec.Write(directory.Path, specification, manifest, ct);
        var text = await File.ReadAllTextAsync(path, ct);
        var json = JsonNode.Parse(text).ShouldNotBeNull().AsObject();
        json[field] = contamination;
        await File.WriteAllTextAsync(path, json.ToJsonString(), ct);

        // Act
        Func<Task> read = () => ReproductionManifestCodec.Read(path, specification, ct);
        var error = await read.ShouldThrow<InvalidDataException>();

        // Assert
        error.Message.ShouldNotContain(contamination, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Duplicate_fields_and_large_files_are_rejected()
    {
        // Arrange
        using var directory = new TemporaryDirectory();
        var ct = TestContext.Current.CancellationToken;
        var (specification, manifest) = ReproductionManifestTestData.Create();
        var path = await ReproductionManifestCodec.Write(directory.Path, specification, manifest, ct);
        var text = await File.ReadAllTextAsync(path, ct);
        Func<Task> read = () => ReproductionManifestCodec.Read(path, specification, ct);

        // Act
        await File.WriteAllTextAsync(path, text.Insert(1, "\"schemaVersion\": 1,"), ct);
        var duplicate = await read.ShouldThrow<InvalidDataException>();
        await File.WriteAllTextAsync(path, text.PadRight(64 * 1024), ct);
        var atLimit = await ReproductionManifestCodec.Read(path, specification, ct);
        await File.AppendAllTextAsync(path, " ", ct);
        var oversized = await read.ShouldThrow<InvalidDataException>();

        // Assert
        atLimit.AttemptId.ShouldBe(manifest.AttemptId);
        duplicate.Message.ShouldBe(oversized.Message);
    }

    [Fact]
    public async Task A_reused_attempt_preserves_the_first_manifest()
    {
        // Arrange
        using var directory = new TemporaryDirectory();
        var ct = TestContext.Current.CancellationToken;
        var (specification, manifest) = ReproductionManifestTestData.Create();
        var path = await ReproductionManifestCodec.Write(directory.Path, specification, manifest, ct);

        // Act
        Func<Task> write = () => ReproductionManifestCodec.Write(directory.Path, specification, manifest with { DurationMs = 200 }, ct);
        await write.ShouldThrow<IOException>();
        var actual = await ReproductionManifestCodec.Read(path, specification, ct);

        // Assert
        actual.DurationMs.ShouldBe(manifest.DurationMs);
        Directory.GetFiles(directory.Path).ShouldBe([path]);
    }
}
