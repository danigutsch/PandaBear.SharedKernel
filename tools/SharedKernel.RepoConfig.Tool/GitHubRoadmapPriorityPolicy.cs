namespace SharedKernel.RepoConfig.Tool;

internal sealed record GitHubRoadmapPriorityPolicy(
    int FirstItemNumber,
    decimal Reach,
    decimal Confidence,
    decimal ImpactCap,
    IReadOnlyDictionary<string, decimal> EffortByLabel,
    decimal DefaultEffort);
