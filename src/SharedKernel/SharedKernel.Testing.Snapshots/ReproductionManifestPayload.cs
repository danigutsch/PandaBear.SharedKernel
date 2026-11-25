namespace SharedKernel.Testing.Snapshots;

/// <summary>The versioned JSON representation of one reproduction attempt.</summary>
/// <param name="SchemaVersion">The manifest schema version.</param>
/// <param name="Revision">The source revision used for the attempt.</param>
/// <param name="Scenario">The registered scenario name.</param>
/// <param name="AttemptId">The opaque attempt identifier.</param>
/// <param name="TraceIds">The W3C trace IDs in lowercase hexadecimal.</param>
/// <param name="Result">The fixture's registered execution result.</param>
/// <param name="OutcomeCode">The registered outcome code.</param>
/// <param name="ResourceStates">The diagnostic resource-state map.</param>
/// <param name="StartedUtc">The start time in UTC.</param>
/// <param name="DurationMs">The duration in milliseconds.</param>
/// <param name="Seed">The optional synthetic generator seed.</param>
internal sealed record ReproductionManifestPayload(
    int SchemaVersion,
    string Revision,
    string Scenario,
    Guid AttemptId,
    string[] TraceIds,
    string Result,
    string OutcomeCode,
    Dictionary<string, string> ResourceStates,
    DateTimeOffset StartedUtc,
    int DurationMs,
    int? Seed = null);
