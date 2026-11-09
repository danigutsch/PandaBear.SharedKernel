namespace PandaBear.Packages;

/// <summary>
/// Decides whether an update may be installed. A candidate that an advisory affects is blocked. Otherwise an installed
/// version that an advisory affects makes the update urgent, whatever the candidate's age. Otherwise the candidate
/// must have been released at least the minimum release age ago. It reads nothing and changes nothing.
/// </summary>
public static class UpdatePolicy
{
    /// <summary>Judges one candidate.</summary>
    /// <param name="candidate">The candidate.</param>
    /// <param name="advisories">Known advisories; those for other packages are ignored.</param>
    /// <param name="minimumAge">How long a release must have been public.</param>
    /// <param name="now">The current time; passed in so the decision is deterministic.</param>
    /// <returns>The decision with its reason.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public static UpdateDecision Decide(UpdateCandidate candidate, IEnumerable<Advisory> advisories, MinimumReleaseAge minimumAge, DateTimeOffset now)
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

        var fixedByCandidate = relevant.Where(a => a.Affects(candidate.InstalledVersion)).Select(static a => a.Id).ToArray();
        if (fixedByCandidate.Length > 0)
        {
            return new UpdateDecision(UpdateDecisionKind.Urgent, candidate, $"The installed version is affected by {string.Join(", ", fixedByCandidate)} and the candidate is not.");
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
