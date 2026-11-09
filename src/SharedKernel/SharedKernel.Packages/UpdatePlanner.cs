using PandaBear.Results;

namespace PandaBear.Packages;

/// <summary>
/// Ties the update policy to its sources: it finds what each package could be updated to, then asks for advisories and
/// release dates and runs <see cref="UpdatePolicy"/>. Any source failure fails the whole operation with that failure, so
/// the policy is never run on an advisory list that is empty only because a lookup failed.
/// </summary>
public sealed class UpdatePlanner
{
    private readonly IAdvisorySource _advisories;
    private readonly IReleaseDateSource _releaseDates;
    private readonly ILatestVersionSource _latest;
    private readonly TimeProvider _time;

    /// <summary>Creates a planner.</summary>
    /// <param name="advisories">Supplies the advisories.</param>
    /// <param name="releaseDates">Supplies the release times of candidates.</param>
    /// <param name="latest">Supplies the newest stable version of each package.</param>
    /// <param name="timeProvider">The clock; the decision time is read from it.</param>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public UpdatePlanner(IAdvisorySource advisories, IReleaseDateSource releaseDates, ILatestVersionSource latest, TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(advisories);
        ArgumentNullException.ThrowIfNull(releaseDates);
        ArgumentNullException.ThrowIfNull(latest);
        ArgumentNullException.ThrowIfNull(timeProvider);
        _advisories = advisories;
        _releaseDates = releaseDates;
        _latest = latest;
        _time = timeProvider;
    }

    /// <summary>
    /// Finds the packages whose newest stable version differs from the installed one, ordered by name. A package whose
    /// newest version equals the installed version is up to date and left out. Versions are not ordered, so a registry
    /// whose newest stable version is older than the installed one is reported as a candidate too.
    /// </summary>
    /// <param name="manager">The package manager that owns the packages.</param>
    /// <param name="installed">The installed packages.</param>
    /// <param name="access">Whether the call may contact the network.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>
    /// The updates and the packages that the registry does not know (<c>NotFound</c>), which are reported and not checked, or the
    /// first other source failure, which fails the whole call.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="installed"/> is <see langword="null"/>.</exception>
    public async Task<Result<UpdateSearch>> FindUpdates(PackageManagerKind manager, IEnumerable<InstalledPackage> installed, NetworkAccess access, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(installed);
        if (access != NetworkAccess.Allowed)
        {
            return Result.Forbidden<UpdateSearch>("The network is not allowed for this call; no update lookup was made.");
        }

        List<PackageUpdate> updates = [];
        List<PackageName> notOnRegistry = [];
        foreach (var package in installed.OrderBy(static p => p.Name.Value, StringComparer.Ordinal))
        {
            var latest = await _latest.GetLatest(manager, package.Name, access, cancellationToken).ConfigureAwait(false);
            if (latest.Status == ResultStatus.NotFound)
            {
                notOnRegistry.Add(package.Name);
                continue;
            }

            if (latest.IsFailure)
            {
                return Result<UpdateSearch>.FromError(Describe(package.Name, latest.ErrorDetails));
            }

            if (latest.Value != package.Version)
            {
                updates.Add(new PackageUpdate(package.Name, package.Version, latest.Value));
            }
        }

        return Result.Ok(new UpdateSearch(updates, notOnRegistry));
    }

    /// <summary>Judges each update against the advisories and the minimum release age, ordered by package name.</summary>
    /// <param name="manager">The package manager that owns the packages.</param>
    /// <param name="updates">The updates to judge.</param>
    /// <param name="minimumAge">How long a release must have been public.</param>
    /// <param name="access">Whether the call may contact the network; <see cref="NetworkAccess.Denied"/> fails before any lookup.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>One decision per update, or the first source failure: no decision is returned when any lookup failed.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="updates"/> or <paramref name="minimumAge"/> is <see langword="null"/>.</exception>
    public async Task<Result<IReadOnlyList<UpdateDecision>>> Plan(PackageManagerKind manager, IEnumerable<PackageUpdate> updates, MinimumReleaseAge minimumAge, NetworkAccess access, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(updates);
        ArgumentNullException.ThrowIfNull(minimumAge);
        if (access != NetworkAccess.Allowed)
        {
            return Result.Forbidden<IReadOnlyList<UpdateDecision>>("The network is not allowed for this call; no advisory or release lookup was made.");
        }

        var ordered = updates.OrderBy(static u => u.Package.Value, StringComparer.Ordinal).ToArray();
        if (ordered.Length == 0)
        {
            return Result.Ok<IReadOnlyList<UpdateDecision>>([]);
        }

        List<InstalledPackage> toCheck = [.. ordered.SelectMany(static u => new[] { new InstalledPackage(u.Package, u.InstalledVersion), new InstalledPackage(u.Package, u.CandidateVersion) })];
        var advisories = await _advisories.Query(manager, toCheck, access, cancellationToken).ConfigureAwait(false);
        if (advisories.IsFailure)
        {
            return Result<IReadOnlyList<UpdateDecision>>.FromError(advisories.ErrorDetails);
        }

        List<UpdateCandidate> candidates = [];
        foreach (var update in ordered)
        {
            var released = await _releaseDates.GetReleaseDate(manager, update.Package, update.CandidateVersion, access, cancellationToken).ConfigureAwait(false);
            if (released.IsFailure)
            {
                return Result<IReadOnlyList<UpdateDecision>>.FromError(Describe(update.Package, released.ErrorDetails));
            }

            candidates.Add(new UpdateCandidate(update.Package, update.InstalledVersion, update.CandidateVersion, released.Value));
        }

        var now = _time.GetUtcNow();
        return Result.Ok<IReadOnlyList<UpdateDecision>>([.. candidates.Select(c => UpdatePolicy.Decide(c, advisories.Value, minimumAge, now))]);
    }

    private static ResultError Describe(PackageName package, ResultError error) => new($"{package}: {error.Detail}", error.Code);
}
