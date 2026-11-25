namespace PandaBear.Repositories;

/// <summary>Identifies the checkout and SDK used to evaluate a restored graph.</summary>
/// <param name="Revision">The checked-out commit.</param>
/// <param name="Dirty">Whether the checkout contains additional changes.</param>
/// <param name="SdkVersion">The selected SDK version.</param>
/// <param name="Solution">The solution path relative to the checkout.</param>
/// <param name="Projects">The evaluated projects and their restore graphs.</param>
/// <param name="Verification">The optional CI receipt for this revision.</param>
internal sealed record NuGetRestoreGraphSource(
    string Revision, bool Dirty, string SdkVersion, string Solution, Dictionary<string, NuGetRestoreProject> Projects, NuGetRestoreVerification? Verification)
{
    /// <summary>File-based apps whose declared dependencies were evaluated in the current trial.</summary>
    internal IReadOnlyList<NuGetFileApp> FileApps { get; init; } = [];
}
