using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;

namespace PandaBear.RepoConfig.Tool;

internal sealed class GitHubIssueReferenceResolver(HttpClient httpClient, TimeProvider? timeProvider = null)
{
    private const int MaximumResponseBytes = 1024 * 1024;
    private const int MaximumRedirects = 5;

    public async Task<GitHubIssueReferenceResolution> Resolve(
        string repository,
        int number,
        string? nodeId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ValidateRepository(repository);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(number);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(120), timeProvider ?? TimeProvider.System);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(timeout.Token, cancellationToken);
        try
        {
            if (!string.IsNullOrWhiteSpace(nodeId))
            {
                var resolved = await ResolveNode(repository, number, nodeId, linked.Token).ConfigureAwait(false);
                if (resolved is not null)
                {
                    return resolved;
                }
            }

            return await ResolveLocation(repository, number, nodeId, linked.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw new OperationCanceledException(cancellationToken);
        }
        catch (OperationCanceledException) when (timeout.IsCancellationRequested)
        {
            throw new TimeoutException("GitHub issue reference resolution timed out after 120 seconds.");
        }
    }

    private async Task<GitHubIssueReferenceResolution?> ResolveNode(
        string repository, int number, string nodeId, CancellationToken cancellationToken)
    {
        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            writer.WriteString("query", "query($id:ID!){node(id:$id){__typename ... on Issue{id number repository{nameWithOwner}}}}");
            writer.WritePropertyName("variables");
            writer.WriteStartObject();
            writer.WriteString("id", nodeId);
            writer.WriteEndObject();
            writer.WriteEndObject();
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.github.com/graphql")
        {
            Content = new StringContent(Encoding.UTF8.GetString(buffer.ToArray()), Encoding.UTF8, "application/json")
        };
        request.Headers.Add("X-Github-Next-Global-ID", "1");
        using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        if (response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Forbidden or HttpStatusCode.Gone)
        {
            RejectRateLimit(response);
            return null;
        }

        RequireSuccess(response);
        using var document = await Read(response, cancellationToken).ConfigureAwait(false);
        var root = document.RootElement;
        if (root.TryGetProperty("errors", out var errors) && errors.GetArrayLength() != 0)
        {
            if (errors.EnumerateArray().All(error => error.TryGetProperty("type", out var type) && type.GetString() == "NOT_FOUND"))
            {
                return null;
            }

            throw new InvalidOperationException("GitHub issue node lookup failed; no reference was changed.");
        }

        if (!root.TryGetProperty("data", out var data) || !data.TryGetProperty("node", out var node))
        {
            throw new InvalidOperationException("GitHub issue node lookup returned incomplete metadata.");
        }

        if (node.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        if (RequiredString(node, "__typename") != "Issue")
        {
            throw new InvalidOperationException("GitHub reference resolved to a pull request or unsupported node.");
        }

        var currentRepository = RequiredString(node.GetProperty("repository"), "nameWithOwner");
        ValidateRepository(currentRepository);
        var currentId = RequiredString(node, "id");
        return new GitHubIssueReferenceResolution(currentRepository, RequiredNumber(node), currentId,
            currentId == nodeId ? null : nodeId, null);
    }

    private async Task<GitHubIssueReferenceResolution> ResolveLocation(
        string repository, int number, string? nodeId, CancellationToken cancellationToken)
    {
        var location = new Uri($"https://api.github.com/repos/{repository}/issues/{number.ToString(CultureInfo.InvariantCulture)}");
        HashSet<string> visited = new(StringComparer.Ordinal);
        for (var redirects = 0; redirects <= MaximumRedirects; redirects++)
        {
            if (!visited.Add(location.AbsoluteUri))
            {
                throw new InvalidOperationException("GitHub issue reference redirect loop detected.");
            }

            using var request = new HttpRequestMessage(HttpMethod.Get, location);
            using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
            if (response.StatusCode is HttpStatusCode.MovedPermanently or HttpStatusCode.Redirect
                or HttpStatusCode.TemporaryRedirect or HttpStatusCode.PermanentRedirect)
            {
                var target = response.Headers.Location
                    ?? throw new InvalidOperationException("GitHub issue transfer response omitted its destination.");

                location = target.IsAbsoluteUri ? target : new Uri(location, target);
                ValidateApiLocation(location);
                continue;
            }

            if (response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Forbidden or HttpStatusCode.Gone)
            {
                RejectRateLimit(response);
                return new GitHubIssueReferenceResolution(repository, number, nodeId, null,
                    $"GitHub issue {repository}#{number.ToString(CultureInfo.InvariantCulture)} is inaccessible or unresolved (HTTP {(int)response.StatusCode}); its mapping and relationships were preserved.");
            }

            RequireSuccess(response);
            using var document = await Read(response, cancellationToken).ConfigureAwait(false);
            var root = document.RootElement;
            if (root.TryGetProperty("pull_request", out _))
            {
                throw new InvalidOperationException("GitHub reference resolved to a pull request.");
            }

            var currentNumber = RequiredNumber(root);
            var currentId = RequiredString(root, "node_id");
            var currentRepository = ReadRepositoryFromIssueUrl(RequiredString(root, "html_url"), currentNumber);
            return new GitHubIssueReferenceResolution(currentRepository, currentNumber, currentId,
                nodeId is not null && currentId != nodeId ? nodeId : null, null);
        }

        throw new InvalidOperationException("GitHub issue reference exceeded the transfer redirect limit.");
    }

    private static string ReadRepositoryFromIssueUrl(string value, int number)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri)
            || uri.Scheme != Uri.UriSchemeHttps || !uri.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase)
            || !uri.IsDefaultPort || uri.UserInfo.Length != 0 || uri.Query.Length != 0 || uri.Fragment.Length != 0)
        {
            throw new InvalidOperationException("GitHub issue reference returned an untrusted issue URL.");
        }

        var parts = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 4 || parts[2] != "issues"
            || parts[3] != number.ToString(CultureInfo.InvariantCulture))
        {
            throw new InvalidOperationException("GitHub issue reference returned inconsistent issue metadata.");
        }

        var repository = $"{parts[0]}/{parts[1]}";
        ValidateRepository(repository);
        return repository;
    }

    private static void ValidateApiLocation(Uri uri)
    {
        var parts = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var validPath = parts.Length == 5 && parts[0] == "repos" && parts[3] == "issues"
            && IsPositiveInteger(parts[4]) && IsRepositoryPart(parts[1]) && IsRepositoryPart(parts[2])
            || parts.Length == 4 && parts[0] == "repositories" && IsPositiveInteger(parts[1])
            && parts[2] == "issues" && IsPositiveInteger(parts[3]);
        if (uri.Scheme != Uri.UriSchemeHttps || !uri.Host.Equals("api.github.com", StringComparison.OrdinalIgnoreCase)
            || !uri.IsDefaultPort || uri.UserInfo.Length != 0 || uri.Query.Length != 0 || uri.Fragment.Length != 0 || !validPath)
        {
            throw new InvalidOperationException("GitHub issue transfer returned an untrusted redirect destination.");
        }
    }

    private static bool IsPositiveInteger(string value) => int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var number) && number > 0;

    private static void ValidateRepository(string repository)
    {
        var parts = repository.Split('/');
        if (parts.Length != 2 || !parts.All(IsRepositoryPart))
        {
            throw new InvalidOperationException("GitHub issue reference requires a repository shaped as owner/repository.");
        }
    }

    private static bool IsRepositoryPart(string value) => value.Length > 0 && value is not "." and not ".."
        && value.All(character => char.IsAsciiLetterOrDigit(character) || character is '-' or '_' or '.');

    private static void RejectRateLimit(HttpResponseMessage response)
    {
        if (response.Headers.RetryAfter is not null
            || response.Headers.TryGetValues("X-RateLimit-Remaining", out var remaining) && remaining.Contains("0"))
        {
            throw new InvalidOperationException("GitHub issue reference resolution was rate limited; no reference was changed.");
        }
    }

    private static void RequireSuccess(HttpResponseMessage response)
    {
        if (!response.IsSuccessStatusCode)
        {
            RejectRateLimit(response);
            throw new InvalidOperationException($"GitHub issue reference resolution failed: HTTP {(int)response.StatusCode}.");
        }
    }

    private static async Task<JsonDocument> Read(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var bytes = await BoundedHttpContentReader.Read(response.Content, MaximumResponseBytes,
            "GitHub issue reference response exceeded the allowed size.", cancellationToken).ConfigureAwait(false);
        return JsonDocument.Parse(bytes);
    }

    private static int RequiredNumber(JsonElement element) => element.TryGetProperty("number", out var value)
        && value.TryGetInt32(out var number) && number > 0 ? number
        : throw new InvalidOperationException("GitHub issue reference returned an invalid issue number.");

    private static string RequiredString(JsonElement element, string property) => element.TryGetProperty(property, out var value)
        && value.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(value.GetString()) ? value.GetString()!
        : throw new InvalidOperationException("GitHub issue reference returned incomplete metadata.");
}
