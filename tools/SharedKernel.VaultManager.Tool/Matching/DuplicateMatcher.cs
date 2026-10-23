using PandaBear.VaultManager.Tool.Merge;
using PandaBear.VaultManager.Tool.Model;

namespace PandaBear.VaultManager.Tool.Matching;

/// <summary>
/// Groups items that describe the same account. Duplicates within one input and across inputs use the same rules.
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item>Logins match when they share at least one normalized host and have an equal, non-empty normalized user name.</item>
/// <item>Items with no host or no user name match other such items on normalized title plus type.</item>
/// <item>Matches are transitive. Forced links are applied first; forced splits are never crossed.</item>
/// <item>Weaker matches become suggestions: same host with different user names, and same title otherwise.</item>
/// </list>
/// </remarks>
internal static class DuplicateMatcher
{
    /// <summary>Groups duplicate items.</summary>
    /// <param name="items">Items from every input, in input order.</param>
    /// <param name="overrides">Forced links and splits, or <see langword="null"/> for none.</param>
    /// <returns>Groups covering every item once, plus suggestions.</returns>
    public static MatchResult Match(IReadOnlyList<VaultItem> items, MatchOverrides? overrides = null)
    {
        ArgumentNullException.ThrowIfNull(items);

        return Match(items, overrides, OriginKeys.Compute(items));
    }

    /// <summary>
    /// Groups duplicate items under origin keys the caller computed earlier, for example before secret placement changed
    /// the items, so decisions keep naming the items as they were read.
    /// </summary>
    /// <param name="items">Items from every input, in input order.</param>
    /// <param name="overrides">Forced links and splits, or <see langword="null"/> for none.</param>
    /// <param name="originKeys">Unique origin key of each item, aligned with <paramref name="items"/>.</param>
    /// <returns>Groups covering every item once, plus suggestions.</returns>
    /// <exception cref="ArgumentException">The keys are not aligned with the items or are not unique.</exception>
    public static MatchResult Match(IReadOnlyList<VaultItem> items, MatchOverrides? overrides, IReadOnlyList<string> originKeys)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(originKeys);
        overrides ??= MatchOverrides.None;

        if (originKeys.Count != items.Count)
        {
            throw new ArgumentException("Every item needs exactly one origin key.", nameof(originKeys));
        }

        var keys = originKeys;
        var indexByKey = new Dictionary<string, int>(StringComparer.Ordinal);
        for (int index = 0; index < keys.Count; index++)
        {
            if (!indexByKey.TryAdd(keys[index], index))
            {
                throw new ArgumentException("Origin keys must be unique.", nameof(originKeys));
            }
        }

        var unionFind = new ConstrainedUnionFind(items.Count, Resolve(overrides.Splits, indexByKey));
        foreach (var (first, second) in Resolve(overrides.Links, indexByKey))
        {
            unionFind.Union(first, second);
        }

        var hosts = items.Select(Hosts).ToArray();
        var usernames = items.Select(item => ItemNormalizer.Username(item.Login?.Username)).ToArray();
        var credentialBuckets = new Dictionary<(string Host, string Username), List<int>>();
        var titleBuckets = new Dictionary<(VaultItemType Type, string Title), List<int>>();
        for (int index = 0; index < items.Count; index++)
        {
            if (items[index].Type == VaultItemType.Login && hosts[index].Count > 0 && usernames[index].Length > 0)
            {
                foreach (string host in hosts[index])
                {
                    JoinBucket(credentialBuckets, (host, usernames[index]), index, unionFind);
                }
            }
            else
            {
                string title = ItemNormalizer.Title(items[index].Name);
                if (title.Length > 0)
                {
                    JoinBucket(titleBuckets, (items[index].Type, title), index, unionFind);
                }
            }
        }

        var groups = BuildGroups(items, keys, unionFind);
        var suggestions = Suggest(items, keys, hosts, usernames, unionFind);
        return new MatchResult(groups, suggestions);
    }

    private static List<(int First, int Second)> Resolve(IReadOnlyList<OriginKeyPair> pairs, Dictionary<string, int> indexByKey)
    {
        var resolved = new List<(int First, int Second)>();
        foreach (var pair in pairs)
        {
            if (indexByKey.TryGetValue(pair.First, out int first) && indexByKey.TryGetValue(pair.Second, out int second))
            {
                resolved.Add((first, second));
            }
        }

        return resolved;
    }

    private static void JoinBucket<TKey>(Dictionary<TKey, List<int>> buckets, TKey key, int index, ConstrainedUnionFind unionFind)
        where TKey : notnull
    {
        if (!buckets.TryGetValue(key, out var bucket))
        {
            buckets[key] = [index];
            return;
        }

        foreach (int previous in bucket)
        {
            unionFind.Union(previous, index);
        }

        bucket.Add(index);
    }

    private static List<string> Hosts(VaultItem item)
    {
        var hosts = new List<string>();
        foreach (var uri in item.Login?.Uris ?? [])
        {
            if (ItemNormalizer.Host(uri.Value) is { } host && !hosts.Contains(host, StringComparer.Ordinal))
            {
                hosts.Add(host);
            }
        }

        return hosts;
    }

    private static List<MatchGroup> BuildGroups(IReadOnlyList<VaultItem> items, IReadOnlyList<string> keys, ConstrainedUnionFind unionFind)
    {
        var members = new Dictionary<int, List<int>>();
        var order = new List<int>();
        for (int index = 0; index < items.Count; index++)
        {
            int root = unionFind.Find(index);
            if (!members.TryGetValue(root, out var list))
            {
                members[root] = list = [];
                order.Add(root);
            }

            list.Add(index);
        }

        var groups = new List<MatchGroup>(order.Count);
        foreach (int root in order)
        {
            var indexes = members[root];
            VaultItem[] groupItems = [.. indexes.Select(index => items[index])];
            string[] groupKeys = [.. indexes.Select(index => keys[index])];
            var status = groupItems.Length == 1
                ? MatchStatus.Unique
                : FieldDiff.ChangedPaths(groupItems).Count == 0 ? MatchStatus.Identical : MatchStatus.Different;
            groups.Add(new MatchGroup(GroupKey(groupKeys), status, groupItems, groupKeys));
        }

        return groups;
    }

    private static string GroupKey(IEnumerable<string> memberKeys) =>
        OriginKeys.Hex16(string.Join('\n', memberKeys.Order(StringComparer.Ordinal)));

    private static List<MatchSuggestion> Suggest(
        IReadOnlyList<VaultItem> items,
        IReadOnlyList<string> keys,
        List<string>[] hosts,
        string[] usernames,
        ConstrainedUnionFind unionFind)
    {
        var suggestions = new List<MatchSuggestion>();
        var suggested = new HashSet<(int, int)>();

        var byHost = new Dictionary<string, List<int>>(StringComparer.Ordinal);
        for (int index = 0; index < items.Count; index++)
        {
            foreach (string host in hosts[index])
            {
                AddPairs(byHost, host, index, (previous, current) =>
                {
                    if (!string.Equals(usernames[previous], usernames[current], StringComparison.Ordinal))
                    {
                        Add(MatchSuggestionKind.SameHostDifferentUsername, previous, current);
                    }
                });
            }
        }

        var byTitle = new Dictionary<string, List<int>>(StringComparer.Ordinal);
        for (int index = 0; index < items.Count; index++)
        {
            string title = ItemNormalizer.Title(items[index].Name);
            if (title.Length > 0)
            {
                AddPairs(byTitle, title, index, (previous, current) => Add(MatchSuggestionKind.SameTitle, previous, current));
            }
        }

        return suggestions;

        void AddPairs(Dictionary<string, List<int>> buckets, string key, int index, Action<int, int> onPair)
        {
            if (!buckets.TryGetValue(key, out var bucket))
            {
                buckets[key] = bucket = [];
            }

            foreach (int previous in bucket)
            {
                if (unionFind.Find(previous) != unionFind.Find(index))
                {
                    onPair(previous, index);
                }
            }

            bucket.Add(index);
        }

        void Add(MatchSuggestionKind kind, int first, int second)
        {
            if (suggested.Add((first, second)))
            {
                suggestions.Add(new MatchSuggestion(kind, keys[first], keys[second], items[first].Name, items[second].Name));
            }
        }
    }
}
