namespace PandaBear.CI;

/// <summary>The validated options of one <c>prune-prereleases</c> run.</summary>
/// <param name="Repository">The repository as <c>owner/name</c>, which names it in the report.</param>
/// <param name="Keep">How many of the newest prereleases stay, at least 1.</param>
/// <param name="Protected">Package versions, without the <c>v</c> of their tags, whose prereleases always stay.</param>
/// <param name="Force">Whether to delete the releases; otherwise the run only previews.</param>
public sealed record PrereleasePruningRequest(string Repository, int Keep, IReadOnlyList<string> Protected, bool Force);
