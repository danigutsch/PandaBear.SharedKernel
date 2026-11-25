namespace PandaBear.Repositories;

/// <summary>A resolved package in one framework and runtime target.</summary>
/// <param name="Version">The selected package version.</param>
/// <param name="Classification">How the package enters the project.</param>
/// <param name="ContentHash">The NuGet SHA-512 package hash.</param>
/// <param name="Dependencies">Sorted dependency IDs and requested versions.</param>
/// <param name="Assets">Sorted selected asset categories and paths.</param>
internal sealed record NuGetRestorePackage(
    string Version,
    NuGetRestoreClassification Classification,
    string ContentHash,
    IReadOnlyList<KeyValuePair<string, string>> Dependencies,
    IReadOnlyList<KeyValuePair<string, string>> Assets)
{
    /// <summary>Compares the resolved package data, including both collections.</summary>
    internal bool SameAs(NuGetRestorePackage other) =>
        Version == other.Version
        && Classification == other.Classification
        && ContentHash == other.ContentHash
        && Dependencies.Count == other.Dependencies.Count
        && Dependencies.Zip(other.Dependencies).All(pair =>
            StringComparer.OrdinalIgnoreCase.Equals(pair.First.Key, pair.Second.Key)
            && pair.First.Value == pair.Second.Value)
        && Assets.SequenceEqual(other.Assets);
}
