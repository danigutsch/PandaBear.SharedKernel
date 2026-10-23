namespace PandaBear.VaultManager.Tool.Pipeline;

/// <summary>A problem placement reported for an accepted finding, described without values.</summary>
/// <param name="ItemName">Name of the input item.</param>
/// <param name="OriginKey">Origin key of the input item.</param>
/// <param name="Problem">What happened, starting with the finding identifier.</param>
internal sealed record PlacementProblem(string ItemName, string OriginKey, string Problem);
