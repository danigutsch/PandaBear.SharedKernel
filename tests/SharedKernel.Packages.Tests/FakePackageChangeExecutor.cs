using SharedKernel.Results;

namespace SharedKernel.Packages.Tests;

/// <summary>Executes fixture state changes without starting a process.</summary>
internal sealed class FakePackageChangeExecutor(Func<PackageManagerKind, PackageChange, CancellationToken, Result> execute)
    : IPackageChangeExecutor
{
    /// <inheritdoc />
    public Task<Result> Apply(PackageManagerKind manager, PackageChange change, CancellationToken cancellationToken) =>
        Task.FromResult(execute(manager, change, cancellationToken));
}
