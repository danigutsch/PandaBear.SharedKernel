namespace PandaBear.CI;

/// <summary>The properties of a forge release that prerelease pruning reads.</summary>
/// <param name="Id">The forge's numeric identifier of the release.</param>
/// <param name="TagName">The tag of the release, such as <c>v1.0.0-alpha.1</c>.</param>
/// <param name="Draft">Whether the release is an unpublished draft.</param>
/// <param name="Prerelease">Whether the release is marked as a prerelease.</param>
/// <param name="CreatedAt">When the release was created.</param>
public sealed record RepositoryRelease(long Id, string TagName, bool Draft, bool Prerelease, DateTimeOffset CreatedAt);
