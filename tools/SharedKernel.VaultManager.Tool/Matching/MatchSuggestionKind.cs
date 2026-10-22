namespace PandaBear.VaultManager.Tool.Matching;

/// <summary>Why two items were suggested as possible duplicates.</summary>
internal enum MatchSuggestionKind
{
    /// <summary>The items share a host but have different user names.</summary>
    SameHostDifferentUsername = 0,

    /// <summary>The items share a normalized title but differ in type, host, or user name.</summary>
    SameTitle = 1,
}
