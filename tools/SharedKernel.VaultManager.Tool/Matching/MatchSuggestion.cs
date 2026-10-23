namespace PandaBear.VaultManager.Tool.Matching;

/// <summary>A weak match between two items in different groups. Suggestions are reported and never applied.</summary>
/// <param name="Kind">Why the items were suggested.</param>
/// <param name="FirstOriginKey">Origin key of the earlier item.</param>
/// <param name="SecondOriginKey">Origin key of the later item.</param>
/// <param name="FirstName">Name of the earlier item.</param>
/// <param name="SecondName">Name of the later item.</param>
internal sealed record MatchSuggestion(MatchSuggestionKind Kind, string FirstOriginKey, string SecondOriginKey, string FirstName, string SecondName);
