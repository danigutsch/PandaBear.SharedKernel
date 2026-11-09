using PandaBear.Results;

namespace PandaBear.Packages.Tests;

/// <summary>An advisory source that returns a fixed result and records what it was asked.</summary>
internal sealed class FakeAdvisorySource(Result<IReadOnlyList<Advisory>> result) : IAdvisorySource
{
    /// <summary>Gets the package versions of the last query.</summary>
    public IReadOnlyCollection<InstalledPackage>? Asked
    {
        get; private set;
    }

    /// <summary>Gets how many queries were made.</summary>
    public int Calls
    {
        get; private set;
    }

    /// <inheritdoc />
    public Task<Result<IReadOnlyList<Advisory>>> Query(PackageManagerKind manager, IReadOnlyCollection<InstalledPackage> packages, NetworkAccess access, CancellationToken cancellationToken)
    {
        Calls++;
        Asked = packages;
        return Task.FromResult(result);
    }
}
