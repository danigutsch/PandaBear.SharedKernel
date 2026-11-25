namespace SharedKernel.CI.Forgejo;

/// <summary>The attachments of the release that <c>GET /repos/{owner}/{repo}/releases/tags/{tag}</c> returns.</summary>
/// <param name="Assets">The attachments of the release.</param>
internal sealed record ForgejoTaggedRelease(ForgejoReleaseAsset[] Assets);
