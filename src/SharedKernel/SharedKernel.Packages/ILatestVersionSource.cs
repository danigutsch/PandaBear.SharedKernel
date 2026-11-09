using PandaBear.Results;

namespace PandaBear.Packages;

/// <summary>A source of the newest stable version of a package.</summary>
public interface ILatestVersionSource
{
    /// <summary>Finds the newest stable version that the package's registry publishes.</summary>
    /// <param name="manager">The package manager that owns the package.</param>
    /// <param name="name">The package name.</param>
    /// <param name="access">Whether the call may contact the network.</param>
    /// <param name="cancellationToken">Cancels the call; cancellation throws.</param>
    /// <returns>
    /// The version, or a failure: <c>Forbidden</c> when the network is denied, <c>Invalid</c> when the manager has no
    /// source, <c>NotFound</c> when the package or a stable version does not exist, <c>Unavailable</c> otherwise.
    /// </returns>
    Task<Result<PackageVersion>> GetLatest(PackageManagerKind manager, PackageName name, NetworkAccess access, CancellationToken cancellationToken);
}
