namespace PandaBear.RepoConfig.Tool;

/// <summary>Roadmap item id and vocabulary rules read from the repository configuration.</summary>
/// <param name="ItemIdPrefix">Prefix every roadmap item id must start with.</param>
/// <param name="AllowedTypes">Item types a roadmap item may use.</param>
/// <param name="AllowedStatuses">Statuses a roadmap item may use.</param>
internal sealed record RoadmapSettings(string ItemIdPrefix, IReadOnlyList<string> AllowedTypes, IReadOnlyList<string> AllowedStatuses)
{
    /// <summary>Settings used when the repository configuration does not supply any.</summary>
    public static RoadmapSettings Default
    {
        get;
    } = new(
        "RM",
        ["epic", "issue", "feature", "enabler", "blocker", "risk", "documentation"],
        ["proposed", "ready", "in_progress", "done", "dropped"]);
}
