namespace PandaBear.Repositories;

/// <summary>Defines the owner scope, deterministic ordering, and resolved-version proof for audit trials.</summary>
internal static class NuGetAuditMinimum
{
    /// <summary>The maximum number of direct owners considered in an automatic plan.</summary>
    internal const int OwnerLimit = 12;

    /// <summary>The maximum number of exact candidate sets evaluated for one plan.</summary>
    internal const int TrialLimit = 32;

    /// <summary>Finds direct advisories, owners of transitive advisories, and declared family companions.</summary>
    internal static string[]? Owners(NuGetRestoreGraphSource baseline, IReadOnlyCollection<NuGetTrialAdvisory> advisories, IReadOnlyList<string> companions)
    {
        var owners = new HashSet<string>(companions, StringComparer.OrdinalIgnoreCase);
        foreach (var advisory in advisories)
        {
            var related = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var (name, project) in baseline.Projects)
            {
                if (advisory.Project is not null && !string.Equals(name, advisory.Project, StringComparison.Ordinal))
                {
                    continue;
                }

                var graph = project.Graph!;
                var affectedDirect = graph.FindPackage(advisory.Package).Any(selection =>
                    selection.Package.Classification == NuGetRestoreClassification.Direct
                    && string.Equals(selection.Package.Version, advisory.Version, StringComparison.OrdinalIgnoreCase));
                if (affectedDirect)
                {
                    related.Add(advisory.Package);
                }

                related.UnionWith(graph.FindDirectOwners(advisory.Package, advisory.Version));
            }

            if (related.Count == 0)
            {
                return null;
            }

            owners.UnionWith(related);
        }

        return owners.Order(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    /// <summary>Uses the actual source settings of every project that directly selects an affected owner.</summary>
    internal static NuGetVersionRequestPackage[]? Requests(NuGetRestoreGraphSource baseline, string[] owners)
    {
        List<NuGetVersionRequestPackage> requests = [];
        foreach (var owner in owners)
        {
            var current = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            List<NuGetVersionSources> sources = [];
            foreach (var project in baseline.Projects.Values)
            {
                var direct = Direct(project, owner).Select(selection => selection.Package.Version).ToArray();
                if (direct.Length == 0)
                {
                    continue;
                }

                if (project.Sources is null)
                {
                    return null;
                }

                current.UnionWith(direct);
                sources.Add(project.Sources);
            }

            if (sources.Count == 0)
            {
                return null;
            }

            requests.Add(new(owner, current.Order(StringComparer.OrdinalIgnoreCase).ToArray(), [.. sources]));
        }

        return [.. requests];
    }

    /// <summary>Yields the smallest owner sets first, then owner ID and ascending SDK version order.</summary>
    internal static IEnumerable<KeyValuePair<string, string>[]> Candidates(NuGetVersionSet[] catalogs, bool includeUnchanged)
    {
        var available = catalogs.Where(catalog => catalog.Versions.Length != 0)
            .OrderBy(catalog => catalog.Package, StringComparer.OrdinalIgnoreCase).ToArray();
        for (var count = includeUnchanged ? 0 : 1; count <= available.Length; count++)
        {
            var owners = new NuGetVersionSet[count];
            foreach (var candidate in OwnersOfSize(available, owners, 0, 0))
            {
                yield return candidate;
            }
        }
    }

    /// <summary>Stops at the first verified or indeterminate candidate, or when the attempt limit is reached.</summary>
    internal static async Task<(string Status, int Attempts)> Search(
        IEnumerable<KeyValuePair<string, string>[]> candidates,
        Func<KeyValuePair<string, string>[], CancellationToken, Task<NuGetAuditAttemptResult>> evaluate, CancellationToken ct)
    {
        var attempts = 0;
        foreach (var candidate in candidates)
        {
            ct.ThrowIfCancellationRequested();
            if (attempts == TrialLimit)
            {
                return ("limitReached", attempts);
            }

            attempts++;
            switch (await evaluate(candidate, ct).ConfigureAwait(false))
            {
                case NuGetAuditAttemptResult.Rejected:
                    continue;
                case NuGetAuditAttemptResult.Verified:
                    return ("proven", attempts);
                default:
                    return ("candidateUnavailable", attempts);
            }
        }

        return ("exhausted", attempts);
    }

    /// <summary>Requires the requested exact resolved version in every target directly selecting an edited owner.</summary>
    internal static bool Matches(NuGetRestoreGraphSource baseline, NuGetRestoreGraphSource candidate, KeyValuePair<string, string>[] updates)
    {
        foreach (var (package, version) in updates)
        {
            foreach (var (name, before) in baseline.Projects)
            {
                var previous = Direct(before, package).Select(item => item.Target).ToArray();
                if (previous.Length == 0)
                {
                    continue;
                }

                if (!candidate.Projects.TryGetValue(name, out var after)
                    || !previous.SequenceEqual(Direct(after, package).Where(item =>
                        string.Equals(item.Package.Version, version, StringComparison.OrdinalIgnoreCase)).Select(item => item.Target), StringComparer.Ordinal))
                {
                    return false;
                }
            }
        }

        return true;
    }

    private static IEnumerable<KeyValuePair<string, string>[]> OwnersOfSize(NuGetVersionSet[] available, NuGetVersionSet[] owners, int offset, int depth)
    {
        if (depth == owners.Length)
        {
            foreach (var candidate in Versions(owners, new KeyValuePair<string, string>[owners.Length], 0))
            {
                yield return candidate;
            }

            yield break;
        }

        for (var index = offset; index <= available.Length - (owners.Length - depth); index++)
        {
            owners[depth] = available[index];
            foreach (var candidate in OwnersOfSize(available, owners, index + 1, depth + 1))
            {
                yield return candidate;
            }
        }
    }

    private static IEnumerable<KeyValuePair<string, string>[]> Versions(NuGetVersionSet[] owners, KeyValuePair<string, string>[] versions, int depth)
    {
        if (depth == owners.Length)
        {
            yield return [.. versions];
            yield break;
        }

        var owner = owners[depth];
        foreach (var version in owner.Versions)
        {
            versions[depth] = new(owner.Package, version);
            foreach (var candidate in Versions(owners, versions, depth + 1))
            {
                yield return candidate;
            }
        }
    }

    private static IEnumerable<(string Target, NuGetRestorePackage Package)> Direct(NuGetRestoreProject project, string package) =>
        project.Graph!.FindPackage(package).Where(selection => selection.Package.Classification == NuGetRestoreClassification.Direct);
}
