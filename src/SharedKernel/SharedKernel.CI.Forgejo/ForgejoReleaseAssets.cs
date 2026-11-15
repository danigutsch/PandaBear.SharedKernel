using System.Net.Http.Headers;
using PandaBear.Forgejo;

namespace PandaBear.CI.Forgejo;

/// <summary>
/// Locates the download address of a Forgejo release attachment: it reads the release of the tag with
/// <c>GET /repos/{owner}/{repo}/releases/tags/{tag}</c> and returns the <c>browser_download_url</c> of the attachment with
/// that name. The release of each tag is read once.
/// </summary>
public sealed class ForgejoReleaseAssets : IReleaseAssets, IDisposable
{
    private readonly HttpClient _client;
    private readonly string _tagsPath;
    private readonly Dictionary<string, ForgejoReleaseAsset[]> _releases = new(StringComparer.Ordinal);

    /// <summary>Creates the locator for the releases of <paramref name="repository"/> on <paramref name="server"/>.</summary>
    /// <param name="handler">Sends the lookups, without credentials; the locator disposes it.</param>
    /// <param name="server">The HTTPS address of the Forgejo instance, such as <c>https://codeberg.org</c>.</param>
    /// <param name="repository">The repository as <c>owner/name</c>.</param>
    /// <param name="userAgent">Names the caller, such as <c>sharedkernel-ci/1.0</c>.</param>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="server"/> is not an HTTPS address, or <paramref name="repository"/> is not <c>owner/name</c>.</exception>
    public ForgejoReleaseAssets(HttpMessageHandler handler, Uri server, string repository, ProductInfoHeaderValue userAgent)
    {
        ArgumentNullException.ThrowIfNull(handler);
        ArgumentNullException.ThrowIfNull(server);
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(userAgent);
        if (!ForgejoRepositoryName.TryParse(repository, out var owner, out var name))
        {
            throw new ArgumentException("The repository must be owner/name.", nameof(repository));
        }

        _tagsPath = "repos/" + Uri.EscapeDataString(owner) + "/" + Uri.EscapeDataString(name) + "/releases/tags/";
        _client = ForgejoApiClient.Create(handler, server, userAgent, token: null);
        _client.Timeout = Timeout.InfiniteTimeSpan;
    }

    /// <inheritdoc />
    /// <exception cref="ArgumentNullException"><paramref name="tag"/> or <paramref name="asset"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidDataException">The release is not valid JSON.</exception>
    public async Task<Uri> Locate(string tag, string asset, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(tag);
        ArgumentNullException.ThrowIfNull(asset);
        var uri = new Uri(_client.BaseAddress!, _tagsPath + Uri.EscapeDataString(tag));
        if (!_releases.TryGetValue(tag, out var assets))
        {
            var release = await ForgejoResponses.Get(_client, uri, ForgejoJsonContext.Default.ForgejoTaggedRelease, "a release", static (value, _) => value, ct).ConfigureAwait(false);
            assets = release.Assets.Any(static item => item is null)
                ? throw new InvalidDataException($"GET {uri} returned a release that is not valid: an attachment is null.")
                : release.Assets;
            _releases[tag] = assets;
        }

        return assets.FirstOrDefault(item => string.Equals(item.Name, asset, StringComparison.Ordinal))?.BrowserDownloadUrl
            ?? throw new HttpRequestException($"The release {uri} has no attachment named {asset}.", null, System.Net.HttpStatusCode.NotFound);
    }

    /// <summary>Disposes the client and its handler.</summary>
    public void Dispose() => _client.Dispose();
}
