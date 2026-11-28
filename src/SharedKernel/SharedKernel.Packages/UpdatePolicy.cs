namespace SharedKernel.Packages;

/// <summary>
/// Judges proposed updates using known package version precedence, explicit advisory fixes, and the minimum release age.
/// </summary>
public static class UpdatePolicy
{
    /// <summary>Judges a candidate whose version ordering the caller has already established.</summary>
    /// <param name="candidate">The candidate.</param>
    /// <param name="advisories">Known advisories; those for other packages are ignored.</param>
    /// <param name="minimumAge">How long a release must have been public.</param>
    /// <param name="now">The current time; passed in so the decision is deterministic.</param>
    /// <returns>The decision with its reason.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public static UpdateDecision Decide(UpdateCandidate candidate, IEnumerable<Advisory> advisories, MinimumReleaseAge minimumAge, DateTimeOffset now) =>
        DecideCore(null, candidate, advisories, minimumAge, now, null);

    /// <summary>Judges one candidate using the manager's known version precedence and an optional pin.</summary>
    /// <param name="manager">The package manager. Cargo and npm have known SemVer precedence.</param>
    /// <param name="candidate">The candidate to judge.</param>
    /// <param name="advisories">Known advisories for the candidate and installed versions.</param>
    /// <param name="minimumAge">How long a release must have been public without an explicit fix.</param>
    /// <param name="now">The evaluation time.</param>
    /// <param name="pin">The configured pin, when reviewing a proposed change to it.</param>
    /// <returns>The decision with its reason. The decision does not change a declared pin.</returns>
    /// <exception cref="ArgumentNullException">The candidate, advisories, or minimum age is <see langword="null"/>.</exception>
    public static UpdateDecision Decide(
        PackageManagerKind manager, UpdateCandidate candidate, IEnumerable<Advisory> advisories,
        MinimumReleaseAge minimumAge, DateTimeOffset now, PackagePin? pin = null) =>
        DecideCore(manager, candidate, advisories, minimumAge, now, pin);

    private static UpdateDecision DecideCore(
        PackageManagerKind? manager, UpdateCandidate candidate, IEnumerable<Advisory> advisories,
        MinimumReleaseAge minimumAge, DateTimeOffset now, PackagePin? pin)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        ArgumentNullException.ThrowIfNull(advisories);
        ArgumentNullException.ThrowIfNull(minimumAge);

        var relevant = advisories.Where(a => a.Package == candidate.Package).OrderBy(static a => a.Id, StringComparer.Ordinal).ToArray();
        var affectingCandidate = relevant.Where(a => a.Affects(candidate.CandidateVersion)).Select(static a => a.Id).ToArray();
        if (affectingCandidate.Length > 0)
        {
            return new UpdateDecision(UpdateDecisionKind.Block, candidate, $"The candidate is affected by {string.Join(", ", affectingCandidate)}.");
        }

        if (manager is { } packageManager)
        {
            var installedOrder = PackageVersionPrecedence.Compare(packageManager, candidate.CandidateVersion, candidate.InstalledVersion);
            if (installedOrder is not > 0)
            {
                var reason = installedOrder is null
                    ? "Version precedence is unknown for this candidate. The candidate must be newer than the installed version."
                    : "The candidate must be newer than the installed version.";
                return new UpdateDecision(UpdateDecisionKind.Hold, candidate, reason);
            }

            if (pin is not null && PackageVersionPrecedence.Compare(packageManager, candidate.CandidateVersion, pin.Version) is not > 0)
            {
                return new UpdateDecision(UpdateDecisionKind.Hold, candidate, "The candidate is not known to be newer than the pinned version.");
            }

            var fixedByCandidate = relevant.SelectMany(advisory => advisory.AffectedRanges
                    .Where(range => range.FixedVersion is { } fixedVersion
                        && range.Contains(candidate.InstalledVersion)
                        && PackageVersionPrecedence.CompareSemVer(candidate.CandidateVersion, fixedVersion) is >= 0)
                    .Select(range => $"{advisory.Id} (fixed in {range.FixedVersion})"))
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            if (fixedByCandidate.Length > 0)
            {
                return new UpdateDecision(UpdateDecisionKind.Urgent, candidate,
                    $"The candidate includes the documented fixes for {string.Join(", ", fixedByCandidate)}. The minimum release age is waived.");
            }
        }
        else
        {
            var affectingInstalled = relevant.Where(a => a.Affects(candidate.InstalledVersion)).Select(static a => a.Id).ToArray();
            if (affectingInstalled.Length > 0)
            {
                return new UpdateDecision(UpdateDecisionKind.Urgent, candidate,
                    $"The installed version is affected by {string.Join(", ", affectingInstalled)} and the candidate is not.");
            }
        }

        var age = now - candidate.ReleasedAt;
        if (age >= minimumAge.Value)
        {
            return new UpdateDecision(UpdateDecisionKind.Allow, candidate, $"The candidate has been public for {Math.Max(age.Days, 0)} days; the minimum is {minimumAge}.");
        }

        // From the release date, so a date in the future waits until the minimum age has passed after that date.
        var days = (int)Math.Ceiling((candidate.ReleasedAt + minimumAge.Value - now).TotalDays);
        return new UpdateDecision(UpdateDecisionKind.Hold, candidate, $"The candidate is too new; it can be installed in {days} {(days == 1 ? "day" : "days")}.");
    }
}
