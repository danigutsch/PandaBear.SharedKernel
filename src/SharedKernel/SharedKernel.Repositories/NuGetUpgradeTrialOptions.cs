namespace SharedKernel.Repositories;

/// <summary>Specifies a dependency trial and the review artifacts to produce.</summary>
/// <param name="Root">The Git checkout to inspect.</param>
/// <param name="PlanPath">The new patch path, with a JSON report at the same path plus <c>.json</c>.</param>
/// <param name="PropsPath">The repository-relative file that owns the selected central declarations.</param>
/// <param name="RemovePin">An optional transitive pin to remove.</param>
/// <param name="Updates">Explicit central updates in <c>package@version</c> form.</param>
/// <param name="Vulnerable">Search exact central updates that resolve reported advisories under the SDK policy.</param>
/// <param name="LocalSources">Repository-relative local package sources to copy with the checkout.</param>
/// <param name="Build">Run the Release solution build after the candidate locked restore.</param>
/// <param name="Test">Run the test solution after the Release build.</param>
public sealed record NuGetUpgradeTrialOptions(
    string Root, string PlanPath, string PropsPath, string? RemovePin, IReadOnlyList<string> Updates,
    bool Vulnerable, IReadOnlyList<string> LocalSources, bool Build, bool Test)
{
    /// <summary>Ask the SDK to propose direct owner updates for the selected pin.</summary>
    public bool ProposePinUpdates { get; init; }

    /// <summary>Additional evaluated direct family members considered with the pin or audit owners.</summary>
    public IReadOnlyList<string> CoordinatedPackages { get; init; } = [];
}
