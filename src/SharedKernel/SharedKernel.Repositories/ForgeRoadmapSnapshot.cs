namespace SharedKernel.Repositories;

/// <summary>A read-only roadmap projection and its verification issues.</summary>
public sealed class ForgeRoadmapSnapshot
{
    private readonly IReadOnlyList<RoadmapItemSnapshot> _work;
    private readonly Dictionary<string, RoadmapItemSnapshot> _items;
    private readonly bool _hasBlockers;

    /// <summary>Creates a snapshot of the candidate work and resolved references.</summary>
    internal ForgeRoadmapSnapshot(
        IReadOnlyList<RoadmapItemSnapshot> work, IReadOnlyList<RoadmapItemSnapshot> items,
        IReadOnlyList<VerificationIssue> issues, bool hasBlockers)
    {
        _work = work;
        _items = items.ToDictionary(static item => item.Id, StringComparer.OrdinalIgnoreCase);
        Issues = issues;
        _hasBlockers = hasBlockers;
    }

    /// <summary>Gets verification diagnostics.</summary>
    public IReadOnlyList<VerificationIssue> Issues { get; }

    /// <summary>Finds one successfully resolved issue for migration comparison.</summary>
    internal RoadmapItemSnapshot? FindItem(string key) => _items.GetValueOrDefault(key);

    /// <summary>Gets open, triaged work by order, RICE score and stable issue key.</summary>
    /// <exception cref="InvalidOperationException">The snapshot is incomplete or blocking data is unavailable for the requested query.</exception>
    public IReadOnlyList<RoadmapItemSnapshot> OpenItems(bool unblockedOnly = false)
    {
        CheckQuery(unblockedOnly);
        return [.. _work.Where(item => item.IsTriaged && item.Status == "open"
                && (!unblockedOnly || item.BlockedBy.All(key => _items.TryGetValue(key, out var blocker) && blocker.Status == "closed")))
            .OrderByPriority()];
    }

    /// <summary>Gets the resolved blockers for a stable forge issue key.</summary>
    /// <exception cref="InvalidOperationException">The snapshot is incomplete, the issue is unknown, or the forge cannot read blockers.</exception>
    public IReadOnlyList<RoadmapItemSnapshot> BlockersOf(string id)
    {
        CheckQuery(needsBlockers: true);
        var key = ForgeRoadmapPriority.ParseReference(id) is { } reference ? ForgeRoadmapPriority.Key(reference) : id;
        var item = _work.FirstOrDefault(item => StringComparer.OrdinalIgnoreCase.Equals(item.Id, key))
            ?? throw new InvalidOperationException("The requested roadmap issue was not loaded.");

        return [.. item.BlockedBy.Select(blocker => _items[blocker])];
    }

    /// <summary>Gets unblocked work, placing issues that unblock other open work before the remaining priorities.</summary>
    /// <exception cref="InvalidOperationException">The snapshot is incomplete or blocking data is unavailable.</exception>
    public IReadOnlyList<RoadmapItemSnapshot> NextWork()
    {
        var meaningfulBlockers = OpenItems().SelectMany(static item => item.BlockedBy).ToHashSet(StringComparer.OrdinalIgnoreCase);
        return [.. OpenItems(unblockedOnly: true).OrderByDescending(item => meaningfulBlockers.Contains(item.Id))];
    }

    private void CheckQuery(bool needsBlockers)
    {
        if (Issues.Count > 0)
        {
            throw new InvalidOperationException("Resolve the forge roadmap verification diagnostics before querying.");
        }

        if (needsBlockers && !_hasBlockers)
        {
            throw new InvalidOperationException("The forge does not supply blocker data.");
        }
    }
}
