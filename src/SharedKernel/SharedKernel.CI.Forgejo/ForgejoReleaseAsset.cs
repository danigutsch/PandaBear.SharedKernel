namespace PandaBear.CI.Forgejo;

/// <summary>The properties of a release attachment that the asset locator reads from the Forgejo releases API.</summary>
/// <param name="Name">The file name of the attachment.</param>
/// <param name="BrowserDownloadUrl">The address that downloads the attachment.</param>
internal sealed record ForgejoReleaseAsset(string Name, Uri BrowserDownloadUrl);
