// Adapted from the ViajantesTurismo reproduction fixtures. See ViajantesTurismo.LICENSE.txt.

using System.Diagnostics;
using System.Text.Json;
using SharedKernel.IO;

namespace SharedKernel.Testing.Snapshots;

/// <summary>Reads and writes bounded manifests using the fixture's reviewed values.</summary>
public static class ReproductionManifestCodec
{
    private const int SchemaVersion = 1;
    private const int MaximumBytes = 64 * 1024;
    private const int MaximumDurationMs = 10 * 60 * 1000;

    /// <summary>Validates the attempt and creates a new manifest named with its opaque attempt ID.</summary>
    /// <param name="resultsDirectory">The caller-owned directory for test results.</param>
    /// <param name="specification">The revision, scenario, seed and possible outcomes approved by the fixture.</param>
    /// <param name="manifest">The metadata observed in the attempt.</param>
    /// <param name="ct">Cancels publication before the file is moved into place.</param>
    /// <returns>The published path.</returns>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    /// <exception cref="InvalidDataException">The manifest exceeds 64 KiB or contains values outside the specification.</exception>
    /// <exception cref="IOException">The file already exists or its contents cannot be published.</exception>
    /// <exception cref="OperationCanceledException">The operation was canceled.</exception>
    public static async Task<string> Write(
        string resultsDirectory, ReproductionSpecification specification, ReproductionManifest manifest, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(specification);
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentNullException.ThrowIfNull(resultsDirectory);
        ct.ThrowIfCancellationRequested();

        var payload = new ReproductionManifestPayload(
            SchemaVersion, manifest.Revision, manifest.Scenario, manifest.AttemptId,
            [.. manifest.TraceIds.Select(static trace => trace.ToHexString())],
            manifest.Outcome.Result, manifest.Outcome.OutcomeCode, new Dictionary<string, string>(manifest.Outcome.ResourceStates, StringComparer.Ordinal),
            manifest.StartedUtc, manifest.DurationMs, manifest.Seed);
        _ = Validate(payload, specification);
        var json = JsonSerializer.SerializeToUtf8Bytes(payload, ReproductionManifestJsonContext.Default.ReproductionManifestPayload);
        if (json.Length > MaximumBytes)
        {
            throw InvalidManifest();
        }

        Directory.CreateDirectory(resultsDirectory);
        var destination = RepositoryPath.Parse($"{payload.AttemptId:N}.json").ResolveUnder(resultsDirectory);
        if (!await AtomicFile.CreateIfAbsent(destination, (stream, _) => stream.WriteAsync(json, ct), ct).ConfigureAwait(false))
        {
            throw new IOException("A reproduction manifest with this attempt ID already exists.");
        }

        return destination;
    }

    /// <summary>Reads one manifest and refuses anything outside the fixture's schema and value allowlist.</summary>
    /// <param name="path">The manifest file.</param>
    /// <param name="specification">The revision, scenario, seed and possible outcomes approved by the fixture.</param>
    /// <param name="ct">Cancels reading.</param>
    /// <returns>The validated metadata.</returns>
    /// <exception cref="ArgumentNullException">The specification is null.</exception>
    /// <exception cref="InvalidDataException">The file exceeds 64 KiB or its contents do not match the schema and specification.</exception>
    /// <exception cref="IOException">The file cannot be read.</exception>
    /// <exception cref="OperationCanceledException">The operation was canceled.</exception>
    public static async Task<ReproductionManifest> Read(string path, ReproductionSpecification specification, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(specification);
        var bytes = await BoundedReader.ReadFile(path, MaximumBytes, ct).ConfigureAwait(false) ?? throw InvalidManifest();
        ReproductionManifestPayload? payload;
        try
        {
            payload = JsonSerializer.Deserialize(bytes, ReproductionManifestJsonContext.Default.ReproductionManifestPayload);
        }
        catch (JsonException)
        {
            throw InvalidManifest();
        }

        return Validate(payload ?? throw InvalidManifest(), specification);
    }

    private static ReproductionManifest Validate(ReproductionManifestPayload payload, ReproductionSpecification specification)
    {
        if (payload.SchemaVersion != SchemaVersion || !IsRevision(payload.Revision) || payload.Revision != specification.Revision
            || payload.Scenario != specification.Scenario || payload.Seed != specification.Seed || payload.AttemptId == Guid.Empty
            || payload.StartedUtc == default || payload.StartedUtc.Offset != TimeSpan.Zero
            || payload.DurationMs is < 0 or > MaximumDurationMs)
        {
            throw InvalidManifest();
        }

        var allowed = specification.AllowedOutcomes.Any(outcome => outcome.Result == payload.Result && outcome.OutcomeCode == payload.OutcomeCode
            && outcome.ResourceStates.Count == payload.ResourceStates.Count
            && outcome.ResourceStates.All(state => payload.ResourceStates.TryGetValue(state.Key, out var observed) && observed == state.Value));
        if (!allowed)
        {
            throw InvalidManifest();
        }

        var traces = payload.TraceIds.Select(ParseTraceId).ToArray();
        if (traces.Distinct().Count() != traces.Length)
        {
            throw InvalidManifest();
        }

        return new ReproductionManifest(
            payload.Revision, payload.Scenario, payload.AttemptId, traces,
            new ReproductionOutcome(payload.Result, payload.OutcomeCode, payload.ResourceStates), payload.StartedUtc, payload.DurationMs, payload.Seed);
    }

    private static ActivityTraceId ParseTraceId(string? value)
    {
        if (value is not { Length: 32 } || !IsNonzeroLowerHex(value))
        {
            throw InvalidManifest();
        }

        return ActivityTraceId.CreateFromString(value);
    }

    private static bool IsRevision([System.Diagnostics.CodeAnalysis.NotNullWhen(true)] string? value) =>
        value is { Length: 40 or 64 } && IsNonzeroLowerHex(value);

    private static bool IsNonzeroLowerHex(string value) =>
        value.All(static character => character is >= '0' and <= '9' or >= 'a' and <= 'f')
        && value.Any(static character => character != '0');

    private static InvalidDataException InvalidManifest() => new("The reproduction manifest does not match the approved fixture.");
}
