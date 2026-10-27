namespace PandaBear.RepoConfig.Tool;

/// <summary>Scoring rules used to prioritize roadmap items.</summary>
/// <param name="FirstItemNumber">Number of the first prioritized item.</param>
/// <param name="Reach">Reach factor.</param>
/// <param name="Confidence">Confidence factor.</param>
/// <param name="ImpactCap">Largest allowed impact.</param>
/// <param name="EffortByLabel">Effort value for each issue label.</param>
/// <param name="DefaultEffort">Effort used when no label applies.</param>
internal sealed record GitHubRoadmapPriorityPolicy(
    int FirstItemNumber,
    decimal Reach,
    decimal Confidence,
    decimal ImpactCap,
    IReadOnlyDictionary<string, decimal> EffortByLabel,
    decimal DefaultEffort);
