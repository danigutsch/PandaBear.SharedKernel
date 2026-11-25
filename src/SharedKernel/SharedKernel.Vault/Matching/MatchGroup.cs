using SharedKernel.Vault.Model;

namespace SharedKernel.Vault.Matching;

/// <summary>Items that describe the same account or record.</summary>
/// <param name="Key">
/// Stable group key: the first 16 lowercase hex characters of SHA-256 over the member origin keys, sorted ordinally
/// and joined with line feeds.
/// </param>
/// <param name="Status">How the members relate.</param>
/// <param name="Members">Member items in input order.</param>
/// <param name="MemberKeys">Origin key of each member, aligned with <paramref name="Members"/>.</param>
public sealed record MatchGroup(string Key, MatchStatus Status, IReadOnlyList<VaultItem> Members, IReadOnlyList<string> MemberKeys);
