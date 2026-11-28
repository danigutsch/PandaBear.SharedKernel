using SharedKernel.Results;

namespace SharedKernel.Packages;

/// <summary>Applies one package change using the caller's selected execution boundary.</summary>
public interface IPackageChangeExecutor
{
    /// <summary>Applies the requested package state for one manager.</summary>
    /// <param name="manager">The manager that owns the package.</param>
    /// <param name="change">The desired condition and its previously observed state.</param>
    /// <param name="cancellationToken">Cancels the attempt.</param>
    /// <returns>The result of the attempt.</returns>
    /// <remarks>
    /// Recheck current state at the execution boundary and treat an already satisfied requirement as success.
    /// If installation is needed and the requirement carries a
    /// <see cref="PackagePin"/>, verify the exact version and artifact checksum before changing installed state.
    /// Return failure when the port cannot safely honor the request.
    /// </remarks>
    Task<Result> Apply(PackageManagerKind manager, PackageChange change, CancellationToken cancellationToken);
}
