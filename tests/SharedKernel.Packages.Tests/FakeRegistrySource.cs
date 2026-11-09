using PandaBear.Results;

namespace PandaBear.Packages.Tests;

/// <summary>A release-date and latest-version source with fixed answers per package name.</summary>
internal sealed class FakeRegistrySource(Dictionary<string, string> latest, Dictionary<string, DateTimeOffset> dates, string? failingDateFor = null, string[]? notFound = null) : IReleaseDateSource, ILatestVersionSource
{
    /// <inheritdoc />
    public Task<Result<PackageVersion>> GetLatest(PackageManagerKind manager, PackageName name, NetworkAccess access, CancellationToken cancellationToken) =>
        Task.FromResult(notFound is not null && notFound.Contains(name.Value)
            ? Result.NotFound<PackageVersion>("not on the registry")
            : latest.TryGetValue(name.Value, out var version)
                ? PackageVersion.Create(version)
                : Result.Unavailable<PackageVersion>("registry is down"));

    /// <inheritdoc />
    public Task<Result<DateTimeOffset>> GetReleaseDate(PackageManagerKind manager, PackageName name, PackageVersion version, NetworkAccess access, CancellationToken cancellationToken) =>
        Task.FromResult(name.Value != failingDateFor && dates.TryGetValue(name.Value, out var date)
            ? Result.Ok(date)
            : Result.Unavailable<DateTimeOffset>("registry is down"));
}
