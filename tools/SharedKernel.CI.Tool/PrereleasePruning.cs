using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text.Json;
using PandaBear.GitHub;
using PandaBear.InputNormalization;
using PandaBear.IO;

namespace PandaBear.CI.Tool;

/// <summary>
/// Keeps the newest prereleases of a GitHub repository and the protected versions, and deletes the other prereleases
/// through the GitHub releases API. Tags are never deleted, and stable releases and drafts are never touched.
/// </summary>
internal static class PrereleasePruning
{
    private const int PageSize = 100;
    private const int MaxPages = 100;
    private const int MaxPageBytes = 16 * 1024 * 1024;

    private static readonly TimeSpan RequestTimeout = TimeSpan.FromMinutes(1);

    private static readonly string ToolVersion =
        typeof(PrereleasePruning).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion.Split('+')[0] ?? "0";

    /// <summary>
    /// Reads every release, writes the plan to <paramref name="output"/>, and with <see cref="PrereleasePruningRequest.Force"/>
    /// deletes the planned releases one at a time, stopping at the first failure.
    /// </summary>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException"><see cref="PrereleasePruningRequest.Force"/> is set without a token.</exception>
    /// <exception cref="HttpRequestException">A request failed; for a deletion the message names the releases already deleted.</exception>
    /// <exception cref="InvalidDataException">A release list is not the expected JSON, or too large.</exception>
    /// <exception cref="TimeoutException">A list request took longer than a minute.</exception>
    public static async Task Run(PrereleasePruningRequest request, PrereleasePruningServices services, TextWriter output, TextWriter error, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(error);

        // gh reads GH_TOKEN before GITHUB_TOKEN. A preview of a public repository works without a token.
        var token = GitHubToken.Find(services.GetEnvironmentVariable);
        if (request.Force && token is null)
        {
            throw new InvalidOperationException(
                $"Set GH_TOKEN or GITHUB_TOKEN to a token that may delete the releases of {request.Repository} (contents: write); --force sent no request.");
        }

        var parts = request.Repository.Split('/');
        var releasesUri = "https://api.github.com/repos/" + Uri.EscapeDataString(parts[0]) + "/" + Uri.EscapeDataString(parts[1]) + "/releases";
        using var client = CreateClient(services.CreateHttpHandler(), token);
        var releases = await List(client, releasesUri, ct).ConfigureAwait(false);
        var plan = PrereleaseRetentionPlan.Create(releases, request.Keep, request.Protected);

        foreach (var version in request.Protected)
        {
            if (!releases.Any(release => string.Equals(release.TagName, "v" + version, StringComparison.Ordinal)))
            {
                await error.WriteLineAsync($"warning: protected version {version} has no release v{version}.").ConfigureAwait(false);
            }
        }

        await output.WriteLineAsync(string.Create(
            CultureInfo.InvariantCulture,
            $"{request.Repository}: {Count(plan.Prereleases.Count, "prerelease")}; keeping the newest {plan.Newest.Count} and {plan.Protected.Count} protected.")).ConfigureAwait(false);
        foreach (var release in plan.Prereleases)
        {
            var action = plan.Newest.Contains(release) ? "keep    {0}  {1}  newest" : plan.Protected.Contains(release) ? "keep    {0}  {1}  protected" : "delete  {0}  {1}";
            await output.WriteLineAsync(string.Format(CultureInfo.InvariantCulture, action, Tag(release), Date(release))).ConfigureAwait(false);
        }

        await output.WriteLineAsync($"Ignored {Count(plan.Stable, "stable release")} and {Count(plan.Drafts, "draft")}.").ConfigureAwait(false);
        if (!request.Force)
        {
            await output.WriteLineAsync($"preview: would delete {Count(plan.Delete.Count, "prerelease")}; --force deletes them. Tags stay.").ConfigureAwait(false);
            return;
        }

        List<string> deleted = [];
        foreach (var release in plan.Delete)
        {
            await Delete(client, releasesUri, release, deleted, ct).ConfigureAwait(false);
            deleted.Add(Tag(release));
            await output.WriteLineAsync("deleted " + Tag(release)).ConfigureAwait(false);
        }

        await output.WriteLineAsync($"Deleted {Count(deleted.Count, "prerelease")}; their tags stay.").ConfigureAwait(false);
    }

    private static string Tag(GitHubRelease release) => ControlCharacters.Escape(release.TagName);

    private static string Date(GitHubRelease release) => release.CreatedAt.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);

    private static string Count(int count, string noun) =>
        string.Create(CultureInfo.InvariantCulture, $"{count} {noun}{(count == 1 ? string.Empty : "s")}");

    private static HttpClient CreateClient(HttpMessageHandler handler, string? token)
    {
        var client = GitHubApiClient.Create(handler, new ProductInfoHeaderValue("sharedkernel-ci", ToolVersion), token);
        client.Timeout = Timeout.InfiniteTimeSpan;
        return client;
    }

    // Every page is read before anything is deleted, so a deletion never shifts a page that is still to be read.
    private static async Task<List<GitHubRelease>> List(HttpClient client, string releasesUri, CancellationToken ct)
    {
        List<GitHubRelease> releases = [];
        for (var page = 1; page <= MaxPages; page++)
        {
            var uri = new Uri(string.Create(CultureInfo.InvariantCulture, $"{releasesUri}?per_page={PageSize}&page={page}"));
            var items = await ReadPage(client, uri, ct).ConfigureAwait(false);
            releases.AddRange(items);
            if (items.Length < PageSize)
            {
                return releases;
            }
        }

        throw new InvalidDataException(string.Create(CultureInfo.InvariantCulture, $"{releasesUri} lists more than {MaxPages * PageSize} releases."));
    }

    private static async Task<GitHubRelease[]> ReadPage(HttpClient client, Uri uri, CancellationToken ct)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(RequestTimeout);
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, uri);
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token).ConfigureAwait(false);
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

    // DELETE /repos/{owner}/{repo}/releases/{id} removes the release and its assets; the tag stays. Nothing is retried: any
    // answer but 204 No Content stops the run, and the message names what was deleted before it.
    private static async Task Delete(HttpClient client, string releasesUri, GitHubRelease release, List<string> deleted, CancellationToken ct)
    {
        var uri = new Uri(releasesUri + "/" + release.Id.ToString(CultureInfo.InvariantCulture));
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(RequestTimeout);
        string failure;
        HttpStatusCode? status = null;
        Exception? inner = null;
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Delete, uri);
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token).ConfigureAwait(false);
            if (response.StatusCode == HttpStatusCode.NoContent)
            {
                return;
            }

            status = response.StatusCode;
            failure = Refused("DELETE", uri, response.StatusCode);
        }
        catch (OperationCanceledException exception) when (!ct.IsCancellationRequested)
        {
            failure = string.Create(CultureInfo.InvariantCulture, $"DELETE {uri} did not finish within {RequestTimeout.TotalSeconds} seconds.");
            inner = exception;
        }
        catch (HttpRequestException exception)
        {
            failure = $"DELETE {uri} failed: {exception.Message}";
            inner = exception;
        }

        // The release may or may not be gone after a timeout or a lost connection, so it is not counted as deleted.
        throw new HttpRequestException(
            $"{failure} The release was {Tag(release)}.{Environment.NewLine}"
            + $"Deleted before the failure: {(deleted.Count == 0 ? "none" : string.Join(", ", deleted))}.{Environment.NewLine}"
            + "No further release was deleted.",
            inner,
            status);
    }

    private static string Refused(string method, Uri uri, HttpStatusCode status) =>
        string.Create(CultureInfo.InvariantCulture, $"{method} {uri} returned {(int)status} {status}.");
}
