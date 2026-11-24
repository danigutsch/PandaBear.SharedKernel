using System.Collections.Immutable;

namespace PandaBear.Review;

/// <summary>Supplies the current review version and ordered evidence for durable storage.</summary>
/// <param name="Revision">The current content revision.</param>
/// <param name="Lanes">The required human lanes in approval order.</param>
/// <param name="Evidence">All recorded decisions in append order.</param>
public sealed record ReviewHistorySnapshot(
    ReviewRevision Revision,
    ImmutableArray<string> Lanes,
    ImmutableArray<ReviewEvidenceSnapshot> Evidence);
