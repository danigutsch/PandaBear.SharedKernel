namespace SharedKernel.RepoConfig.Tool;

internal static class RoadmapItemOrderingExtensions
{
    public static IOrderedEnumerable<RoadmapItemSnapshot> OrderByPriority(this IEnumerable<RoadmapItemSnapshot> items) =>
        items.OrderBy(item => item.Order ?? int.MaxValue).ThenByDescending(item => item.Score).ThenBy(item => item.Id, StringComparer.Ordinal);
}
