using PandaBear.Results;

namespace PandaBear.Packages;

/// <summary>
/// A source of security advisories. A lookup that fails or only partly succeeds is a failed <see cref="Result{T}"/>,
/// never an empty list: an empty success means that the source knows of no advisory for the packages asked about.
/// </summary>
public interface IAdvisorySource
{
    /// <summary>Finds the advisories that affect the given package versions.</summary>
    /// <param name="manager">The package manager that owns the packages; it selects the source's ecosystem.</param>
    /// <param name="packages">The package versions to check, for example the installed and the candidate version of each package.</param>
    /// <param name="access">Whether the call may contact the network.</param>
    /// <param name="cancellationToken">Cancels the call; cancellation throws.</param>
    /// <returns>
    /// Advisories that each list at least the given versions they affect, or a failure: <c>Forbidden</c> when the network
    /// is denied, <c>Invalid</c> when the manager is not supported, <c>Unavailable</c> when the source cannot answer completely.
    /// </returns>
    Task<Result<IReadOnlyList<Advisory>>> Query(
        PackageManagerKind manager,
        IReadOnlyCollection<InstalledPackage> packages,
        NetworkAccess access,
        CancellationToken cancellationToken);
}
