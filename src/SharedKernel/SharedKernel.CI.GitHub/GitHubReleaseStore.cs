using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using PandaBear.GitHub;
using PandaBear.IO;

namespace PandaBear.CI.GitHub;

/// <summary>
/// Lists and deletes the releases of a GitHub repository through the
/// <see href="https://docs.github.com/rest/releases/releases">REST releases API</see>. Every page is read before anything
/// is deleted, each request has a one-minute timeout, and nothing is retried.
/// </summary>
public sealed class GitHubReleaseStore : IReleaseStore, IDisposable
{
    private const int PageSize = 100;
    private const int MaxPages = 100;
    private const int MaxPageBytes = 16 * 1024 * 1024;

    private static readonly TimeSpan RequestTimeout = TimeSpan.FromMinutes(1);

    private readonly HttpClient _client;
    private readonly string _releasesUri;

    /// <summary>Creates the store of the releases of <paramref name="repository"/>.</summary>
    /// <param name="handler">Sends the requests; the store disposes it. It should refuse redirects, so the token never reaches another address.</param>
    /// <param name="repository">The repository as <c>owner/name</c>.</param>
    /// <param name="userAgent">Names the caller, such as <c>sharedkernel-ci/1.0</c>.</param>
    /// <param name="token">The token from <see cref="GitHubToken.Find"/>, which deleting requires, or <see langword="null"/> to list a public repository anonymously.</param>
    /// <exception cref="ArgumentNullException"><paramref name="handler"/>, <paramref name="repository"/>, or <paramref name="userAgent"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="repository"/> is not <c>owner/name</c>, or <paramref name="token"/> is empty.</exception>
    public GitHubReleaseStore(HttpMessageHandler handler, string repository, ProductInfoHeaderValue userAgent, string? token)
    {
        ArgumentNullException.ThrowIfNull(handler);
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(userAgent);
        var (owner, name) = GitHubRepositoryName.Parse(repository, nameof(repository));
        _releasesUri = "https://api.github.com/repos/" + Uri.EscapeDataString(owner) + "/" + Uri.EscapeDataString(name) + "/releases";
        _client = GitHubApiClient.Create(handler, userAgent, token);
        _client.Timeout = Timeout.InfiniteTimeSpan;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<RepositoryRelease>> List(CancellationToken ct)
    {
        List<RepositoryRelease> releases = [];
        for (var page = 1; page <= MaxPages; page++)
        {
            var uri = new Uri(string.Create(CultureInfo.InvariantCulture, $"{_releasesUri}?per_page={PageSize}&page={page}"));
            var items = await ReadPage(uri, ct).ConfigureAwait(false);
            releases.AddRange(items.Select(static item => new RepositoryRelease(item.Id, item.TagName, item.Draft, item.Prerelease, item.CreatedAt)));
            if (items.Length < PageSize)
            {
                return releases;
            }
        }

        throw new InvalidDataException(string.Create(CultureInfo.InvariantCulture, $"{_releasesUri} lists more than {MaxPages * PageSize} releases."));
    }

    /// <inheritdoc />
    /// <remarks><c>DELETE /repos/{owner}/{repo}/releases/{id}</c> removes the release and its assets; the tag stays. Any answer but 204 No Content fails.</remarks>
    /// <exception cref="ArgumentNullException"><paramref name="release"/> is <see langword="null"/>.</exception>
    public async Task Delete(RepositoryRelease release, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(release);
        var uri = new Uri(_releasesUri + "/" + release.Id.ToString(CultureInfo.InvariantCulture));
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(RequestTimeout);
        HttpResponseMessage response;
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Delete, uri);
            response = await _client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException exception) when (!ct.IsCancellationRequested)
        {
            throw new TimeoutException(string.Create(CultureInfo.InvariantCulture, $"DELETE {uri} did not finish within {RequestTimeout.TotalSeconds} seconds."), exception);
        }
        catch (HttpRequestException exception)
        {
            throw new HttpRequestException($"DELETE {uri} failed: {exception.Message}", exception);
        }

        using (response)
        {
            if (response.StatusCode != HttpStatusCode.NoContent)
            {
                throw new HttpRequestException(Refused("DELETE", uri, response.StatusCode), null, response.StatusCode);
            }
        }
    }

    /// <summary>Disposes the client and its handler.</summary>
    public void Dispose() => _client.Dispose();

    private static string Refused(string method, Uri uri, HttpStatusCode status) =>
        string.Create(CultureInfo.InvariantCulture, $"{method} {uri} returned {(int)status} {status}.");

    private static async Task<byte[]> ReadBody(HttpContent content, Uri uri, CancellationToken ct)
    {
        if (content.Headers.ContentLength > MaxPageBytes)
        {
            throw new InvalidDataException($"GET {uri} returned more than {MaxPageBytes} bytes.");
        }

        var source = await content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        await using (source.ConfigureAwait(false))
        {
            return await BoundedReader.ReadToEnd(source, MaxPageBytes, ct).ConfigureAwait(false)
                ?? throw new InvalidDataException($"GET {uri} returned more than {MaxPageBytes} bytes.");
        }
    }

    private async Task<GitHubRelease[]> ReadPage(Uri uri, CancellationToken ct)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(RequestTimeout);
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, uri);
            using var response = await _client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token).ConfigureAwait(false);
            if (response.StatusCode != HttpStatusCode.OK)
            {
                throw new HttpRequestException(Refused("GET", uri, response.StatusCode), null, response.StatusCode);
            }

            var body = await ReadBody(response.Content, uri, timeout.Token).ConfigureAwait(false);
            GitHubRelease[]? items;
            try
            {
                items = JsonSerializer.Deserialize(body, GitHubReleaseJsonContext.Default.GitHubReleaseArray);
            }
            catch (JsonException exception)
            {
                throw new InvalidDataException($"GET {uri} returned a release list that is not valid: {exception.Message}", exception);
            }

            if (items is null || items.Any(static item => item is null))
            {
                throw new InvalidDataException($"GET {uri} returned a release list that is not valid: a release is null.");
            }

            return items;
        }
        catch (OperationCanceledException exception) when (!ct.IsCancellationRequested)
        {
            throw new TimeoutException(
                string.Create(CultureInfo.InvariantCulture, $"GET {uri} did not finish within {RequestTimeout.TotalSeconds} seconds."), exception);
        }
    }
}
