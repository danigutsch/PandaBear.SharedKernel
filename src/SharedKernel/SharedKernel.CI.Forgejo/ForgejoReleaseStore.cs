using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using PandaBear.Forgejo;

namespace PandaBear.CI.Forgejo;

/// <summary>
/// Lists and deletes the releases of a Forgejo repository through the releases API, <c>GET</c> and <c>DELETE</c>
/// <c>/repos/{owner}/{repo}/releases</c> of the <see href="https://forgejo.org/docs/latest/user/api-usage/">Forgejo API</see>.
/// Every page is read before anything is deleted, each request has a one-minute timeout, and nothing is retried.
/// </summary>
public sealed class ForgejoReleaseStore : IReleaseStore, IDisposable
{
    // Forgejo caps limit at max_response_items, 50 unless the instance changes it, so the pages follow its Link header.
    private const int PageSize = 50;
    private const int MaxPages = 200;

    private readonly HttpClient _client;
    private readonly string _releasesPath;

    /// <summary>Creates the store of the releases of <paramref name="repository"/> on <paramref name="server"/>.</summary>
    /// <param name="handler">Sends the requests; the store disposes it. It should refuse redirects, so the token never reaches another address.</param>
    /// <param name="server">The HTTPS address of the Forgejo instance, such as <c>https://codeberg.org</c>.</param>
    /// <param name="repository">The repository as <c>owner/name</c>.</param>
    /// <param name="userAgent">Names the caller, such as <c>sharedkernel-ci/1.0</c>.</param>
    /// <param name="token">The token from <see cref="ForgejoToken.Find"/>, which deleting requires, or <see langword="null"/> to list a public repository anonymously.</param>
    /// <exception cref="ArgumentNullException"><paramref name="handler"/>, <paramref name="server"/>, <paramref name="repository"/>, or <paramref name="userAgent"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="server"/> is not an HTTPS address, <paramref name="repository"/> is not <c>owner/name</c>, or <paramref name="token"/> is empty.</exception>
    public ForgejoReleaseStore(HttpMessageHandler handler, Uri server, string repository, ProductInfoHeaderValue userAgent, string? token)
    {
        ArgumentNullException.ThrowIfNull(handler);
        ArgumentNullException.ThrowIfNull(server);
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(userAgent);
        var (owner, name) = ForgejoRepositoryName.Parse(repository, nameof(repository));
        _releasesPath = "repos/" + Uri.EscapeDataString(owner) + "/" + Uri.EscapeDataString(name) + "/releases";
        _client = ForgejoApiClient.Create(handler, server, userAgent, token);
        _client.Timeout = Timeout.InfiniteTimeSpan;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<RepositoryRelease>> List(CancellationToken ct)
    {
        List<RepositoryRelease> releases = [];
        for (var page = 1; page <= MaxPages; page++)
        {
            var uri = new Uri(_client.BaseAddress!, string.Create(CultureInfo.InvariantCulture, $"{_releasesPath}?limit={PageSize}&page={page}"));
            var (items, link) = await ForgejoResponses.Get(_client, uri, ForgejoJsonContext.Default.ForgejoReleaseArray, "a release list", ct).ConfigureAwait(false);
            if (items.Any(static item => item is null))
            {
                throw new InvalidDataException($"GET {uri} returned a release list that is not valid: a release is null.");
            }

            releases.AddRange(items.Select(static item => new RepositoryRelease(item.Id, item.TagName, item.Draft, item.Prerelease, item.CreatedAt)));
            if (items.Length == 0 || !HasNextPage(link))
            {
                return releases;
            }
        }

        throw new InvalidDataException(string.Create(CultureInfo.InvariantCulture, $"{new Uri(_client.BaseAddress!, _releasesPath)} lists more than {MaxPages} pages of releases."));
    }

    /// <inheritdoc />
    /// <remarks><c>DELETE /repos/{owner}/{repo}/releases/{id}</c> removes the release and its attachments; the tag stays. Any answer but 204 No Content fails.</remarks>
    /// <exception cref="ArgumentNullException"><paramref name="release"/> is <see langword="null"/>.</exception>
    public async Task Delete(RepositoryRelease release, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(release);
        var uri = new Uri(_client.BaseAddress!, _releasesPath + "/" + release.Id.ToString(CultureInfo.InvariantCulture));
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(ForgejoResponses.RequestTimeout);
        HttpResponseMessage response;
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Delete, uri);
            response = await _client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException exception) when (!ct.IsCancellationRequested)
        {
            throw new TimeoutException(
                string.Create(CultureInfo.InvariantCulture, $"DELETE {uri} did not finish within {ForgejoResponses.RequestTimeout.TotalSeconds} seconds."), exception);
        }
        catch (HttpRequestException exception)
        {
            throw new HttpRequestException($"DELETE {uri} failed: {exception.Message}", exception);
        }

        using (response)
        {
            if (response.StatusCode != HttpStatusCode.NoContent)
            {
                throw new HttpRequestException(ForgejoResponses.Refused("DELETE", uri, response.StatusCode), null, response.StatusCode);
            }
        }
    }

    /// <summary>Disposes the client and its handler.</summary>
    public void Dispose() => _client.Dispose();

    // Forgejo sends rel="next" while a later page exists; a missing header means one page.
    private static bool HasNextPage(string? link) =>
        link is not null && link.Split(',').Any(static part => part.Contains("rel=\"next\"", StringComparison.Ordinal));
}
