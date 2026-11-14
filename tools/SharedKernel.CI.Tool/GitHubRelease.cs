namespace PandaBear.CI.Tool;

/// <summary>The properties of a GitHub release that <c>prune-prereleases</c> reads from the releases API.</summary>
/// <param name="Id">The release ID that the delete endpoint takes.</param>
/// <param name="TagName">The tag of the release, such as <c>v1.0.0-alpha.1.1</c>.</param>
/// <param name="Draft">Whether the release is a draft.</param>
/// <param name="Prerelease">Whether the release is marked prerelease.</param>
/// <param name="CreatedAt">When the release was created.</param>
internal sealed record GitHubRelease(long Id, string TagName, bool Draft, bool Prerelease, DateTimeOffset CreatedAt);
