namespace SharedKernel.RepoConfig.Tool;

internal sealed record RoadmapSettings(string ItemIdPrefix, IReadOnlyList<string> AllowedTypes, IReadOnlyList<string> AllowedStatuses)
{
    public static RoadmapSettings Default { get; } = new(
        "RM",
        ["epic", "issue", "feature", "enabler", "blocker", "risk", "documentation"],
        ["proposed", "ready", "in_progress", "done", "dropped"]);
}
