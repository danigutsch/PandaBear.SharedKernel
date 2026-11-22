namespace PandaBear.Testing.Snapshots;

/// <summary>Identifies an approved observation and its diagnostic resource states.</summary>
/// <param name="Result">The fixture's registered execution result.</param>
/// <param name="OutcomeCode">The registered outcome code.</param>
/// <param name="ResourceStates">The approved diagnostic resource states for this outcome.</param>
public sealed record ReproductionOutcome(
    string Result,
    string OutcomeCode,
    IReadOnlyDictionary<string, string> ResourceStates);
