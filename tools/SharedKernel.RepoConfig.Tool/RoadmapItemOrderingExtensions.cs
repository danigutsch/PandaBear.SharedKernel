namespace PandaBear.RepoConfig.Tool;

/// <summary>Ordering helpers for roadmap item snapshots.</summary>
internal static class RoadmapItemOrderingExtensions
{
    /// <summary>Orders items by explicit order (unordered last), then by descending score, then by ordinal id.</summary>
    public static IOrderedEnumerable<RoadmapItemSnapshot> OrderByPriority(this IEnumerable<RoadmapItemSnapshot> items) =>
        items.OrderBy(item => item.Order ?? int.MaxValue).ThenByDescending(item => item.Score).ThenBy(item => item.Id, StringComparer.Ordinal);
}
