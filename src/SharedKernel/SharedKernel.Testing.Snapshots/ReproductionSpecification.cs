namespace SharedKernel.Testing.Snapshots;

/// <summary>Declares the values reviewed for one fixture at a known source revision.</summary>
/// <param name="Scenario">The registered scenario name.</param>
/// <param name="Revision">The exact fixture source revision.</param>
/// <param name="AllowedOutcomes">The fixture's reviewed outcome and resource-state combinations.</param>
/// <param name="Seed">The optional approved synthetic seed.</param>
public sealed record ReproductionSpecification(
    string Scenario,
    string Revision,
    IReadOnlyList<ReproductionOutcome> AllowedOutcomes,
    int? Seed = null);
