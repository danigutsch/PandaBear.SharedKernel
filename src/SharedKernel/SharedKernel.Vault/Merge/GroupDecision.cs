namespace PandaBear.Vault.Merge;

/// <summary>Field decisions and pending entries for one match group.</summary>
/// <param name="GroupKey">Stable group key.</param>
/// <param name="Choices">At most one choice per field path.</param>
public sealed record GroupDecision(string GroupKey, IReadOnlyList<FieldChoice> Choices);
