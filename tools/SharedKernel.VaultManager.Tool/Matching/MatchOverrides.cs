namespace PandaBear.VaultManager.Tool.Matching;

/// <summary>User corrections to automatic matching.</summary>
/// <param name="Links">Pairs that must end up in the same group.</param>
/// <param name="Splits">Pairs that must never end up in the same group. A split wins over a link.</param>
internal sealed record MatchOverrides(IReadOnlyList<OriginKeyPair> Links, IReadOnlyList<OriginKeyPair> Splits)
{
    /// <summary>No overrides.</summary>
    public static MatchOverrides None { get; } = new([], []);
}
