using System.Diagnostics;

namespace PandaBear.Testing.Snapshots;

/// <summary>Describes one fixture attempt using only the manifest's approved values.</summary>
/// <param name="Revision">The source revision used for the attempt.</param>
/// <param name="Scenario">The registered scenario name.</param>
/// <param name="AttemptId">The fresh opaque attempt identifier.</param>
/// <param name="TraceIds">The trace IDs observed by the fixture.</param>
/// <param name="Outcome">The approved outcome and diagnostic resource states.</param>
/// <param name="StartedUtc">The time the attempt started in UTC.</param>
/// <param name="DurationMs">The elapsed attempt duration in milliseconds.</param>
/// <param name="Seed">The synthetic seed when the fixture needs one.</param>
public sealed record ReproductionManifest(
    string Revision,
    string Scenario,
    Guid AttemptId,
    IReadOnlyList<ActivityTraceId> TraceIds,
    ReproductionOutcome Outcome,
    DateTimeOffset StartedUtc,
    int DurationMs,
    int? Seed = null);
