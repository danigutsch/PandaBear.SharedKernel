namespace PandaBear.CI.Tool;

/// <summary>Sorts the releases of a repository into the prereleases that stay and those that <c>prune-prereleases</c> deletes.</summary>
/// <param name="Prereleases">Every published prerelease, newest first: by creation date, then by release ID.</param>
/// <param name="Newest">The newest prereleases that are not protected, at most the number to keep.</param>
/// <param name="Protected">The prereleases whose package version is protected.</param>
/// <param name="Delete">The other prereleases, newest first.</param>
/// <param name="Stable">The number of published releases that are not prereleases; they are never deleted.</param>
/// <param name="Drafts">The number of draft releases; they are never deleted.</param>
internal sealed record PrereleaseRetentionPlan(
    IReadOnlyList<GitHubRelease> Prereleases,
    IReadOnlySet<GitHubRelease> Newest,
    IReadOnlySet<GitHubRelease> Protected,
    IReadOnlyList<GitHubRelease> Delete,
    int Stable,
    int Drafts)
{
    /// <summary>Plans which prereleases stay.</summary>
    /// <param name="releases">Every release of the repository.</param>
    /// <param name="keep">How many of the newest unprotected prereleases stay.</param>
    /// <param name="protectedVersions">Package versions whose <c>v&lt;version&gt;</c> prerelease always stays.</param>
    /// <exception cref="ArgumentNullException"><paramref name="releases"/> or <paramref name="protectedVersions"/> is <see langword="null"/>.</exception>
    public static PrereleaseRetentionPlan Create(IReadOnlyList<GitHubRelease> releases, int keep, IReadOnlyCollection<string> protectedVersions)
    {
        ArgumentNullException.ThrowIfNull(releases);
        ArgumentNullException.ThrowIfNull(protectedVersions);
        var protectedTags = protectedVersions.Select(static version => "v" + version).ToHashSet(StringComparer.Ordinal);

        // Only a published release marked prerelease can be deleted; a draft is never published, even when marked prerelease.
        GitHubRelease[] prereleases = [.. releases
            .Where(static release => release is { Draft: false, Prerelease: true })
            .OrderByDescending(static release => release.CreatedAt)
            .ThenByDescending(static release => release.Id)];
        var isProtected = prereleases.Where(release => protectedTags.Contains(release.TagName)).ToHashSet();
        var unprotected = prereleases.Where(release => !isProtected.Contains(release)).ToArray();
        return new PrereleaseRetentionPlan(
            prereleases,
            unprotected.Take(keep).ToHashSet(),
            isProtected,
            [.. unprotected.Skip(keep)],
            releases.Count(static release => release is { Draft: false, Prerelease: false }),
            releases.Count(static release => release.Draft));
    }
}
