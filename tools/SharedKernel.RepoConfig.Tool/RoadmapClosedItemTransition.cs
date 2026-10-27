namespace PandaBear.RepoConfig.Tool;

/// <summary>
/// A closed GitHub issue approved to move its roadmap item to a closed status.
/// <paramref name="CloseReason"/> is the GitHub close outcome: <see cref="RoadmapCloseReason.Completed"/> or
/// <see cref="RoadmapCloseReason.NotPlanned"/>.
/// </summary>
/// <param name="Issue">GitHub issue number that was closed.</param>
/// <param name="RoadmapItem">Id of the roadmap item to move to a closed status.</param>
/// <param name="CloseReason">GitHub close outcome, completed or not planned.</param>
internal sealed record RoadmapClosedItemTransition(
    int Issue,
    string RoadmapItem,
    string CloseReason = RoadmapCloseReason.Completed);
