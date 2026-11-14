namespace PandaBear.CI.Tool;

/// <summary>The validated options of one <c>prune-prereleases</c> run.</summary>
/// <param name="Repository">The GitHub repository as <c>owner/name</c>.</param>
/// <param name="Keep">How many of the newest prereleases stay, at least 1.</param>
/// <param name="Protected">Package versions, without the <c>v</c> of their tags, whose prereleases always stay.</param>
/// <param name="Force">Whether to delete the releases; otherwise the run only previews.</param>
internal sealed record PrereleasePruningRequest(string Repository, int Keep, IReadOnlyList<string> Protected, bool Force);
