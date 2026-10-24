using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace PandaBear.RepoConfig.Tool;

/// <summary>
/// Links an existing issue to a parent in the same GitHub repository without replacing another parent.
/// </summary>
internal static class GitHubSubissueCommand
{
    private const int MaxResponseBytes = 1024 * 1024;
    private const string ApiVersion = "2022-11-28";

    /// <summary>
    /// Previews a link without network access, or resolves, links, and verifies it when forced.
    /// </summary>
    /// <exception cref="InvalidOperationException">Arguments are invalid, authentication is absent, or GitHub rejects or fails to verify the link.</exception>
    /// <exception cref="TimeoutException">GitHub does not complete the operation within thirty seconds.</exception>
    public static async Task<int> Run(
        string repository,
        int parent,
        int child,
        bool force,
        HttpClient? httpClient,
        TextWriter output,
        CancellationToken cancellationToken)
    {
        if (!GitHubRepositoryName.IsValid(repository))
        {
            throw new InvalidOperationException("--repository must name a GitHub repository as owner/name.");
        }

        if (parent <= 0 || child <= 0)
        {
            throw new InvalidOperationException("--parent and --child must be positive issue numbers.");
        }

        if (parent == child)
        {
            throw new InvalidOperationException("An issue cannot be its own parent.");
        }

        cancellationToken.ThrowIfCancellationRequested();
        var description = string.Create(CultureInfo.InvariantCulture, $"{repository}#{child} to {repository}#{parent}");
        if (!force)
        {
            await output.WriteLineAsync($"preview: would link {description}; remote issues and parent are checked only with --force.".AsMemory(), cancellationToken).ConfigureAwait(false);
            return 0;
        }

        using var ownedClient = httpClient is null ? GitHubHttpClient.Create("subissue linking") : null;
        var client = httpClient ?? ownedClient!;
        using var timeout = new CancellationTokenSource(GitHubRequestTimeout.Duration);
        using var operation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);
        var writeMayHaveSucceeded = false;
        try
        {
            var issuesRoot = string.Create(CultureInfo.InvariantCulture, $"https://api.github.com/repos/{repository}/issues/");
            var parentEndpoint = new Uri(issuesRoot + parent.ToString(CultureInfo.InvariantCulture));
            var childEndpoint = new Uri(issuesRoot + child.ToString(CultureInfo.InvariantCulture));
            var parentIssue = await GetIssue(client, parentEndpoint, operation.Token).ConfigureAwait(false);
            var childIssue = await GetIssue(client, childEndpoint, operation.Token).ConfigureAwait(false);
            var currentParent = await GetParent(client, childEndpoint, operation.Token).ConfigureAwait(false);
            if (currentParent is not null)
            {
                if (currentParent != parentIssue)
                {
                    throw new InvalidOperationException("The child already has a different parent; reparenting is not supported.");
                }

                await output.WriteLineAsync($"Already linked {description}.".AsMemory(), cancellationToken).ConfigureAwait(false);
                return 0;
            }

            using var request = new HttpRequestMessage(HttpMethod.Post, parentEndpoint.AbsoluteUri + "/sub_issues");
            request.Content = new StringContent(
                string.Create(CultureInfo.InvariantCulture, $"{{\"sub_issue_id\":{childIssue.Id},\"replace_parent\":false}}"),
                Encoding.UTF8, "application/json");
            writeMayHaveSucceeded = true;
            using var response = await Send(client, request, operation.Token).ConfigureAwait(false);
            writeMayHaveSucceeded = response.StatusCode == HttpStatusCode.Created;
            // A concurrent identical link may make GitHub return 422. Only a matching readback can make that successful.
            if (response.StatusCode is not HttpStatusCode.Created and not HttpStatusCode.UnprocessableEntity)
            {
                throw StatusFailure(response.StatusCode);
            }

            var verifiedParent = await GetParent(client, childEndpoint, operation.Token).ConfigureAwait(false);
            if (verifiedParent != parentIssue)
            {
                if (response.StatusCode == HttpStatusCode.UnprocessableEntity)
                {
                    throw StatusFailure(response.StatusCode);
                }

                throw new InvalidOperationException("Could not verify the requested parent after linking.");
            }

            await output.WriteLineAsync($"Linked {description}.".AsMemory(), cancellationToken).ConfigureAwait(false);
            return 0;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw new OperationCanceledException(cancellationToken);
        }
        catch (OperationCanceledException) when (timeout.IsCancellationRequested)
        {
            throw new TimeoutException(WithWriteWarning(GitHubRequestTimeout.Create("subissue linking").Message, writeMayHaveSucceeded));
        }
        catch (OperationCanceledException)
        {
            throw new InvalidOperationException(WithWriteWarning("GitHub subissue linking request was unexpectedly canceled.", writeMayHaveSucceeded));
        }
        catch (Exception exception) when (exception is HttpRequestException or IOException)
        {
            throw new InvalidOperationException(WithWriteWarning("GitHub subissue linking request failed.", writeMayHaveSucceeded));
        }
        catch (JsonException)
        {
            throw new InvalidOperationException(WithWriteWarning("GitHub subissue linking returned an invalid issue response.", writeMayHaveSucceeded));
        }
        catch (InvalidOperationException exception) when (writeMayHaveSucceeded)
        {
            throw new InvalidOperationException(WithWriteWarning(exception.Message, writeMayHaveSucceeded));
        }
    }

    private static string WithWriteWarning(string message, bool writeMayHaveSucceeded) => writeMayHaveSucceeded
        ? message + " The remote write may have succeeded. Inspect the child before retrying."
        : message;

    private static async Task<GitHubSubissueIdentity> GetIssue(HttpClient client, Uri endpoint, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, endpoint);
        using var response = await Send(client, request, cancellationToken).ConfigureAwait(false);
        if (response.StatusCode != HttpStatusCode.OK)
        {
            throw StatusFailure(response.StatusCode);
        }

        var issue = await ReadIssue(response, cancellationToken).ConfigureAwait(false);
        return string.Equals(issue.Url, endpoint.AbsoluteUri, StringComparison.OrdinalIgnoreCase)
            ? issue
            : throw InvalidIssue();
    }

    private static async Task<GitHubSubissueIdentity?> GetParent(HttpClient client, Uri childEndpoint, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, childEndpoint.AbsoluteUri + "/parent");
        using var response = await Send(client, request, cancellationToken).ConfigureAwait(false);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            // The caller has already verified the child exists and is readable.
            return null;
        }

        if (response.StatusCode != HttpStatusCode.OK)
        {
            throw StatusFailure(response.StatusCode);
        }

        return await ReadIssue(response, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<HttpResponseMessage> Send(HttpClient client, HttpRequestMessage request, CancellationToken cancellationToken)
    {
        request.Headers.Add("X-GitHub-Api-Version", ApiVersion);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        return await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<GitHubSubissueIdentity> ReadIssue(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var content = await BoundedHttpContentReader.Read(response.Content, MaxResponseBytes,
            "GitHub subissue linking response exceeded the allowed size.", cancellationToken).ConfigureAwait(false);
        using var document = JsonDocument.Parse(content, new JsonDocumentOptions { AllowDuplicateProperties = false });
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object
            || !root.TryGetProperty("id", out var id) || id.ValueKind != JsonValueKind.Number || !id.TryGetInt64(out var identity) || identity <= 0
            || !root.TryGetProperty("number", out var number) || number.ValueKind != JsonValueKind.Number || !number.TryGetInt32(out var issueNumber) || issueNumber <= 0
            || !root.TryGetProperty("url", out var url) || url.ValueKind != JsonValueKind.String
            || root.TryGetProperty("pull_request", out _))
        {
            throw InvalidIssue();
        }

        var location = url.GetString();
        if (!Uri.TryCreate(location, UriKind.Absolute, out var parsed)
            || parsed.Scheme != Uri.UriSchemeHttps || parsed.Host != "api.github.com"
            || parsed.UserInfo.Length != 0 || !parsed.IsDefaultPort || parsed.Query.Length != 0 || parsed.Fragment.Length != 0
            || !parsed.AbsolutePath.EndsWith("/issues/" + issueNumber.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal))
        {
            throw InvalidIssue();
        }

        return new GitHubSubissueIdentity(identity, location!);
    }

    private static InvalidOperationException InvalidIssue() => new("GitHub subissue linking returned an invalid issue response.");

    private static InvalidOperationException StatusFailure(HttpStatusCode statusCode) =>
        new(string.Create(CultureInfo.InvariantCulture, $"GitHub subissue linking failed with HTTP {(int)statusCode}."));
}
