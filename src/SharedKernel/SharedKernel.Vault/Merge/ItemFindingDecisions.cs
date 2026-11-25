namespace SharedKernel.Vault.Merge;

/// <summary>User decisions on the secret findings of one input item.</summary>
/// <param name="OriginKey">Origin key of the item as read, from <see cref="Matching.OriginKeys"/>.</param>
/// <param name="Choices">At most one decision per finding.</param>
/// <param name="ItemName">Name of the item, so the user can find it. Informational: reading decisions ignores it.</param>
public sealed record ItemFindingDecisions(string OriginKey, IReadOnlyList<FindingChoice> Choices, string? ItemName = null);
