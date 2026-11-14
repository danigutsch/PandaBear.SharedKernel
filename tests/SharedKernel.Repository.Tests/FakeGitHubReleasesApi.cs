using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json.Nodes;

namespace PandaBear.Repository.Tests;

/// <summary>
/// Serves the GitHub releases API of one repository from memory: release list pages of 100 and release deletion. Every
/// other request, such as a tag deletion, returns 404 and is recorded, so a test can prove it was never sent.
/// </summary>
internal sealed class FakeGitHubReleasesApi : HttpMessageHandler
{
    /// <summary>The repository that the fake serves.</summary>
    public const string Repository = "octo-org/octo-lib";

    /// <summary>The API address of the repository's releases.</summary>
    public const string ReleasesUrl = "https://api.github.com/repos/" + Repository + "/releases";

    private const int PageSize = 100;

    /// <summary>Gets the creation date of the first release that <see cref="AddPrereleases"/> adds, less an hour.</summary>
    public static DateTimeOffset Start { get; } = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private readonly List<FakeGitHubRelease> _releases = [];
    private readonly Dictionary<long, HttpStatusCode> _deleteFailures = [];
    private readonly Dictionary<int, HttpStatusCode> _pageFailures = [];

    /// <summary>Gets every request that reached the fake, in arrival order.</summary>
    public List<FakeGitHubRequest> Requests { get; } = [];

    /// <summary>Gets the release IDs of the DELETE requests that succeeded, in order.</summary>
    public List<long> Deleted { get; } = [];

    /// <summary>Adds a release and returns its ID; IDs grow in the order releases are added.</summary>
    public long Add(string tag, DateTimeOffset createdAt, bool prerelease = true, bool draft = false)
    {
        var id = 1000 + _releases.Count;
        _releases.Add(new FakeGitHubRelease(id, tag, createdAt, prerelease, draft));
        return id;
    }

    /// <summary>Adds the prereleases <c>v0.1.0-alpha.1.1</c> to <c>v0.1.0-alpha.&lt;count&gt;.1</c>, each an hour newer than the one before.</summary>
    public void AddPrereleases(int count)
    {
        for (var run = 1; run <= count; run++)
        {
            Add(string.Create(CultureInfo.InvariantCulture, $"v0.1.0-alpha.{run}.1"), Start.AddHours(run));
        }
    }

    /// <summary>Makes the DELETE request of release <paramref name="id"/> answer <paramref name="status"/>.</summary>
    public void FailDelete(long id, HttpStatusCode status) => _deleteFailures[id] = status;

    /// <summary>Makes list page <paramref name="page"/> answer <paramref name="status"/>.</summary>
    public void FailPage(int page, HttpStatusCode status) => _pageFailures[page] = status;

    /// <summary>Gets the release ID of <paramref name="tag"/>.</summary>
    public long IdOf(string tag) => _releases.Single(release => release.Tag == tag).Id;

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var uri = request.RequestUri!;
        Requests.Add(new FakeGitHubRequest(
            request.Method,
            uri,
            request.Headers.Authorization?.ToString(),
            request.Headers.UserAgent.ToString(),
            request.Headers.TryGetValues("X-GitHub-Api-Version", out var versions) ? string.Join(',', versions) : null));
        return Task.FromResult(Answer(request.Method, uri));
    }

    private static HttpResponseMessage Status(HttpStatusCode status) => new(status) { Content = new StringContent("{\"message\":\"failure\"}") };

    private HttpResponseMessage Answer(HttpMethod method, Uri uri)
    {
        var path = uri.GetLeftPart(UriPartial.Path);
        if (method == HttpMethod.Get && path == ReleasesUrl)
        {
            var query = System.Web.HttpUtility.ParseQueryString(uri.Query);
            if (query["per_page"] != PageSize.ToString(CultureInfo.InvariantCulture) || !int.TryParse(query["page"], CultureInfo.InvariantCulture, out var page) || page < 1)
            {
                return Status(HttpStatusCode.BadRequest);
            }

            if (_pageFailures.TryGetValue(page, out var failure))
            {
                return Status(failure);
            }

            // GitHub lists releases newest first; the deleted ones are gone.
            var items = _releases.Where(release => !Deleted.Contains(release.Id))
                .Reverse()
                .Skip((page - 1) * PageSize)
                .Take(PageSize)
                .Select(static release => (JsonNode)new JsonObject
                {
                    ["url"] = ReleasesUrl + "/" + release.Id.ToString(CultureInfo.InvariantCulture),
                    ["id"] = release.Id,
                    ["tag_name"] = release.Tag,
                    ["name"] = release.Tag,
                    ["draft"] = release.Draft,
                    ["prerelease"] = release.Prerelease,
                    ["created_at"] = release.CreatedAt.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture),
                    ["published_at"] = release.Draft ? null : release.CreatedAt.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture),
                    ["assets"] = new JsonArray(),
                    ["body"] = "notes",
                })
                .ToArray();
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(new JsonArray(items).ToJsonString(), Encoding.UTF8, "application/json"),
            };
        }

        if (method == HttpMethod.Delete && path.StartsWith(ReleasesUrl + "/", StringComparison.Ordinal)
            && long.TryParse(path[(ReleasesUrl.Length + 1)..], CultureInfo.InvariantCulture, out var id)
            && _releases.Any(release => release.Id == id) && !Deleted.Contains(id))
        {
            if (_deleteFailures.TryGetValue(id, out var failure))
            {
                return Status(failure);
            }

            Deleted.Add(id);
            return new HttpResponseMessage(HttpStatusCode.NoContent);
        }

        return Status(HttpStatusCode.NotFound);
    }
}
