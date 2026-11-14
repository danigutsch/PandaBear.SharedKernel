namespace PandaBear.Repository.Tests;

/// <summary>A release that <see cref="FakeGitHubReleasesApi"/> serves.</summary>
/// <param name="Id">The release ID.</param>
/// <param name="Tag">The tag name, such as <c>v1.0.0-alpha.1.1</c>.</param>
/// <param name="CreatedAt">The creation date.</param>
/// <param name="Prerelease">Whether the release is marked prerelease.</param>
/// <param name="Draft">Whether the release is a draft.</param>
internal sealed record FakeGitHubRelease(long Id, string Tag, DateTimeOffset CreatedAt, bool Prerelease, bool Draft);
