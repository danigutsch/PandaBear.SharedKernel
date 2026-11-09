using PandaBear.Results;

namespace PandaBear.Packages;

/// <summary>A source of the time at which a package version was released.</summary>
public interface IReleaseDateSource
{
    /// <summary>Finds when a version was released.</summary>
    /// <param name="manager">The package manager that owns the package.</param>
    /// <param name="name">The package name.</param>
    /// <param name="version">The version.</param>
    /// <param name="access">Whether the call may contact the network.</param>
    /// <param name="cancellationToken">Cancels the call; cancellation throws.</param>
    /// <returns>
    /// The release time, or a failure: <c>Forbidden</c> when the network is denied, <c>Invalid</c> when the manager has no
    /// release-date source, <c>NotFound</c> when the package or version is unknown, <c>Unavailable</c> otherwise.
    /// </returns>
    Task<Result<DateTimeOffset>> GetReleaseDate(
        PackageManagerKind manager,
        PackageName name,
        PackageVersion version,
        NetworkAccess access,
        CancellationToken cancellationToken);
}
