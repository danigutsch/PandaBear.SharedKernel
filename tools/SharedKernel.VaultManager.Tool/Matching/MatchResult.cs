namespace PandaBear.VaultManager.Tool.Matching;

/// <summary>Result of duplicate matching.</summary>
/// <param name="Groups">Every input item in exactly one group, ordered by the first member's input position.</param>
/// <param name="Suggestions">Weak matches that were not applied.</param>
internal sealed record MatchResult(IReadOnlyList<MatchGroup> Groups, IReadOnlyList<MatchSuggestion> Suggestions);
