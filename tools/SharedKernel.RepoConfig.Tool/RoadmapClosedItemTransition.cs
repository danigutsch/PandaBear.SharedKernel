namespace PandaBear.RepoConfig.Tool;

/// <summary>
/// A closed GitHub issue approved to move its roadmap item to a closed status.
/// <paramref name="CloseReason"/> is the GitHub close outcome: <see cref="RoadmapCloseReason.Completed"/> or
/// <see cref="RoadmapCloseReason.NotPlanned"/>.
/// </summary>
internal sealed record RoadmapClosedItemTransition(
    int Issue,
    string RoadmapItem,
    string CloseReason = RoadmapCloseReason.Completed);
