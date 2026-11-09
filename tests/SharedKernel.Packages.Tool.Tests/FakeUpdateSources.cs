using PandaBear.Results;

namespace PandaBear.Packages.Tool.Tests;

/// <summary>Fixed answers for the three source ports; nothing reaches the network.</summary>
internal sealed class FakeUpdateSources(
    Dictionary<string, string> latest,
    Dictionary<string, DateTimeOffset> dates,
    Result<IReadOnlyList<Advisory>> advisories,
    DateTimeOffset now,
    string[]? notFound = null) : IAdvisorySource, IReleaseDateSource, ILatestVersionSource
{
    /// <summary>Gets how many lookups were made.</summary>
    public int Lookups
    {
        get; private set;
    }

    /// <summary>Creates the sources that the command receives.</summary>
    public UpdateSources ToSources() => new(this, this, this, new FixedTime(now));

    /// <inheritdoc />
    public Task<Result<IReadOnlyList<Advisory>>> Query(PackageManagerKind manager, IReadOnlyCollection<InstalledPackage> packages, NetworkAccess access, CancellationToken cancellationToken)
    {
        Lookups++;
        return Task.FromResult(advisories);
    }

    /// <inheritdoc />
    public Task<Result<DateTimeOffset>> GetReleaseDate(PackageManagerKind manager, PackageName name, PackageVersion version, NetworkAccess access, CancellationToken cancellationToken)
    {
        Lookups++;
        return Task.FromResult(dates.TryGetValue(name.Value, out var date) ? Result.Ok(date) : Result.Unavailable<DateTimeOffset>("registry is down"));
    }

    /// <inheritdoc />
    public Task<Result<PackageVersion>> GetLatest(PackageManagerKind manager, PackageName name, NetworkAccess access, CancellationToken cancellationToken)
    {
        Lookups++;
        if (notFound is not null && notFound.Contains(name.Value))
        {
            return Task.FromResult(Result.NotFound<PackageVersion>("not on the registry"));
        }

        return Task.FromResult(latest.TryGetValue(name.Value, out var version) ? PackageVersion.Create(version) : Result.Unavailable<PackageVersion>("registry is down"));
    }
}
