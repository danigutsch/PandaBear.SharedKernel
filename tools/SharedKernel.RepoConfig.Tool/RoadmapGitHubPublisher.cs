using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace SharedKernel.RepoConfig.Tool;

internal sealed class RoadmapGitHubPublisher
{
    private const string ApiVersion = "2022-11-28";
    private const string BaseBranch = "main";
    private const string CommitMessage = "chore(roadmap): refresh GitHub reconciliation";
    private const int MaxChangedEntries = 512;
    private const int MaxPublicationBytes = 32 * 1024 * 1024;
    private const int MaxResponseBytes = 1024 * 1024;
    private static readonly TimeSpan PublicationTimeout = TimeSpan.FromMinutes(10);
    private const string PullRequestBody = "Refs #1160";

    private readonly HttpClient _httpClient;
    private readonly string _rootPath;
    private readonly TimeProvider _timeProvider;

    public RoadmapGitHubPublisher(string rootPath, HttpClient httpClient)
        : this(rootPath, httpClient, TimeProvider.System)
    {
    }

    internal RoadmapGitHubPublisher(
        string rootPath,
        HttpClient httpClient,
        TimeProvider timeProvider)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootPath);
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(timeProvider);
        _rootPath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(rootPath));
        _httpClient = httpClient;
        _timeProvider = timeProvider;
    }

    public async Task<RoadmapGitHubPublication> Publish(
        RoadmapAutomationPlan plan,
        RoadmapGitChangeSet changes,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(changes);
        if (!changes.HasChanges || changes.Entries.Count == 0)
        {
            throw new InvalidOperationException("Roadmap publication requires a nonempty sealed result.");
        }

        if (changes.Entries.Count > MaxChangedEntries)
        {
            throw new InvalidOperationException("Roadmap publication exceeds the allowed changed-path count.");
        }

        if (!GitHubRepositoryName.IsValid(plan.Repository))
        {
            throw new InvalidOperationException("Roadmap publication repository is invalid.");
        }

        using var publicationTimeout = new CancellationTokenSource(PublicationTimeout, _timeProvider);
        using var publicationCancellation = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            publicationTimeout.Token);
        try
        {
            RoadmapGitChangeGuard.Verify(_rootPath, changes);
            var files = await ReadPublicationFiles(changes.Entries, publicationCancellation.Token).ConfigureAwait(false);
            RoadmapGitChangeGuard.Verify(_rootPath, changes);
            var endpoint = CreateRepositoryEndpoint(plan.Repository);
            var initialDefaultOid = await ReadDefaultBranchOid(endpoint, publicationCancellation.Token).ConfigureAwait(false);
            RequireDefaultOid(plan.BaseSha, initialDefaultOid, "Roadmap publication default branch moved after result verification.");
            var baseTreeSha = await ReadBaseTreeSha(endpoint, plan.BaseSha, publicationCancellation.Token).ConfigureAwait(false);
            var treeEntries = await CreateTreeEntries(endpoint, files, publicationCancellation.Token).ConfigureAwait(false);
            var treeSha = await CreateTree(endpoint, baseTreeSha, treeEntries, publicationCancellation.Token).ConfigureAwait(false);
            var commitSha = await CreateVerifiedCommit(endpoint, plan.BaseSha, treeSha, publicationCancellation.Token).ConfigureAwait(false);
            RoadmapGitChangeGuard.Verify(_rootPath, changes);
            var prePublicationDefaultOid = await ReadDefaultBranchOid(endpoint, publicationCancellation.Token).ConfigureAwait(false);
            RequireDefaultOid(plan.BaseSha, prePublicationDefaultOid, "Roadmap publication default branch moved after result verification.");
            await CreateReference(endpoint, plan.BranchName, commitSha, publicationCancellation.Token).ConfigureAwait(false);
            var postPublicationDefaultOid = await ReadDefaultBranchOid(endpoint, publicationCancellation.Token).ConfigureAwait(false);
            RequireDefaultOid(plan.BaseSha, postPublicationDefaultOid, "Roadmap publication default branch moved during branch creation.");
            var pullRequestUrl = await CreatePullRequest(
                endpoint,
                plan,
                commitSha,
                publicationCancellation.Token).ConfigureAwait(false);
            return new RoadmapGitHubPublication(pullRequestUrl, commitSha);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw new OperationCanceledException(cancellationToken);
        }
        catch (OperationCanceledException) when (publicationTimeout.IsCancellationRequested)
        {
            throw GitHubRequestTimeout.Create("roadmap publication", PublicationTimeout);
        }
    }

    private async Task<string> ReadDefaultBranchOid(Uri endpoint, CancellationToken cancellationToken)
    {
        using var document = await Send(
            HttpMethod.Get,
            new Uri(endpoint, $"git/ref/heads/{BaseBranch}"),
            writeBody: null,
            HttpStatusCode.OK,
            "Roadmap publication could not read the default branch.",
            cancellationToken).ConfigureAwait(false);
        return RequireSha(RequireObject(document.RootElement, "object"), "sha");
    }

    private async Task<string> ReadBaseTreeSha(
        Uri endpoint,
        string baseSha,
        CancellationToken cancellationToken)
    {
        using var document = await Send(
            HttpMethod.Get,
            new Uri(endpoint, $"git/commits/{baseSha}"),
            writeBody: null,
            HttpStatusCode.OK,
            "Roadmap publication could not read the approved base commit.",
            cancellationToken).ConfigureAwait(false);
        var responseSha = RequireSha(document.RootElement, "sha");
        if (!string.Equals(responseSha, baseSha, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Roadmap publication base commit does not match the approved plan.");
        }

        return RequireSha(RequireObject(document.RootElement, "tree"), "sha");
    }

    private async Task<PublicationFileEntry[]> ReadPublicationFiles(
        IReadOnlyList<RoadmapGitStatusEntry> changes,
        CancellationToken cancellationToken)
    {
        Dictionary<string, PublicationFileEntry> entries = new(StringComparer.Ordinal);
        long totalBytes = 0;
        foreach (var change in changes.OrderBy(change => change.Path, StringComparer.Ordinal))
        {
            if (change.OriginalPath is not null)
            {
                AddFileEntry(entries, new PublicationFileEntry(change.OriginalPath, "100644", Content: null));
            }

            var fullPath = Path.GetFullPath(Path.Combine(
                _rootPath,
                change.Path.Replace('/', Path.DirectorySeparatorChar)));
            if (!File.Exists(fullPath))
            {
                AddFileEntry(entries, new PublicationFileEntry(change.Path, "100644", Content: null));
                continue;
            }

            if (change.WorkTreeMode is not "100644" and not "100755")
            {
                throw new InvalidOperationException("Roadmap publication accepts regular file modes only.");
            }

            var content = await File.ReadAllBytesAsync(fullPath, cancellationToken).ConfigureAwait(false);
            totalBytes += content.Length;
            if (totalBytes > MaxPublicationBytes)
            {
                throw new InvalidOperationException("Roadmap publication exceeds the allowed content size.");
            }

            AddFileEntry(entries, new PublicationFileEntry(change.Path, change.WorkTreeMode, content));
        }

        return entries.Values.OrderBy(entry => entry.Path, StringComparer.Ordinal).ToArray();
    }

    private async Task<PublicationTreeEntry[]> CreateTreeEntries(
        Uri endpoint,
        IReadOnlyList<PublicationFileEntry> files,
        CancellationToken cancellationToken)
    {
        List<PublicationTreeEntry> entries = [];
        foreach (var file in files)
        {
            var blobSha = file.Content is null
                ? null
                : await CreateBlob(endpoint, file.Content, cancellationToken).ConfigureAwait(false);
            entries.Add(new PublicationTreeEntry(file.Path, file.Mode, blobSha));
        }

        return entries.ToArray();
    }

    private async Task<string> CreateBlob(Uri endpoint, byte[] content, CancellationToken cancellationToken)
    {
        using var document = await Send(
            HttpMethod.Post,
            new Uri(endpoint, "git/blobs"),
            writer =>
            {
                writer.WriteString("content", Convert.ToBase64String(content));
                writer.WriteString("encoding", "base64");
            },
            HttpStatusCode.Created,
            "Roadmap publication could not create a Git blob.",
            cancellationToken).ConfigureAwait(false);
        return RequireSha(document.RootElement, "sha");
    }

    private async Task<string> CreateTree(
        Uri endpoint,
        string baseTreeSha,
        IReadOnlyList<PublicationTreeEntry> entries,
        CancellationToken cancellationToken)
    {
        using var document = await Send(
            HttpMethod.Post,
            new Uri(endpoint, "git/trees"),
            writer =>
            {
                writer.WriteString("base_tree", baseTreeSha);
                writer.WritePropertyName("tree");
                writer.WriteStartArray();
                foreach (var entry in entries)
                {
                    writer.WriteStartObject();
                    writer.WriteString("path", entry.Path);
                    writer.WriteString("mode", entry.Mode);
                    writer.WriteString("type", "blob");
                    if (entry.Sha is null)
                    {
                        writer.WriteNull("sha");
                    }
                    else
                    {
                        writer.WriteString("sha", entry.Sha);
                    }

                    writer.WriteEndObject();
                }

                writer.WriteEndArray();
            },
            HttpStatusCode.Created,
            "Roadmap publication could not create a Git tree.",
            cancellationToken).ConfigureAwait(false);
        if (document.RootElement.TryGetProperty("truncated", out var truncated)
            && truncated.ValueKind == JsonValueKind.True)
        {
            throw new InvalidOperationException("Roadmap publication Git tree response was truncated.");
        }

        return RequireSha(document.RootElement, "sha");
    }

    private async Task<string> CreateVerifiedCommit(
        Uri endpoint,
        string baseSha,
        string treeSha,
        CancellationToken cancellationToken)
    {
        using var document = await Send(
            HttpMethod.Post,
            new Uri(endpoint, "git/commits"),
            writer =>
            {
                writer.WriteString("message", CommitMessage);
                writer.WriteString("tree", treeSha);
                writer.WritePropertyName("parents");
                writer.WriteStartArray();
                writer.WriteStringValue(baseSha);
                writer.WriteEndArray();
            },
            HttpStatusCode.Created,
            "Roadmap publication could not create the App commit.",
            cancellationToken).ConfigureAwait(false);
        var verification = RequireObject(document.RootElement, "verification");
        if (!RequireBoolean(verification, "verified")
            || !string.Equals(RequireString(verification, "reason"), "valid", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Roadmap publication requires a GitHub-verified App commit.");
        }

        var responseTreeSha = RequireSha(RequireObject(document.RootElement, "tree"), "sha");
        var parents = RequireArray(document.RootElement, "parents");
        if (!string.Equals(responseTreeSha, treeSha, StringComparison.Ordinal)
            || parents.GetArrayLength() != 1
            || parents[0].ValueKind != JsonValueKind.Object
            || !string.Equals(
                RequireSha(parents[0], "sha"),
                baseSha,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Roadmap publication commit does not match the approved tree and sole parent.");
        }

        return RequireSha(document.RootElement, "sha");
    }

    private async Task CreateReference(
        Uri endpoint,
        string branchName,
        string commitSha,
        CancellationToken cancellationToken)
    {
        using var document = await Send(
            HttpMethod.Post,
            new Uri(endpoint, "git/refs"),
            writer =>
            {
                writer.WriteString("ref", $"refs/heads/{branchName}");
                writer.WriteString("sha", commitSha);
            },
            HttpStatusCode.Created,
            "Roadmap publication could not create a new branch.",
            cancellationToken,
            branchCollisionMessage: "Roadmap publication could not create a new branch; the ref already exists or was rejected.").ConfigureAwait(false);
        var createdRef = RequireString(document.RootElement, "ref");
        var createdSha = RequireSha(RequireObject(document.RootElement, "object"), "sha");
        if (!string.Equals(createdRef, $"refs/heads/{branchName}", StringComparison.Ordinal)
            || !string.Equals(createdSha, commitSha, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Roadmap publication created branch does not match the approved commit.");
        }
    }

    private async Task<string> CreatePullRequest(
        Uri endpoint,
        RoadmapAutomationPlan plan,
        string commitSha,
        CancellationToken cancellationToken)
    {
        using var document = await Send(
            HttpMethod.Post,
            new Uri(endpoint, "pulls"),
            writer =>
            {
                writer.WriteString("title", CommitMessage);
                writer.WriteString("head", plan.BranchName);
                writer.WriteString("base", BaseBranch);
                writer.WriteString("body", PullRequestBody);
                writer.WriteBoolean("maintainer_can_modify", false);
                writer.WriteBoolean("draft", true);
            },
            HttpStatusCode.Created,
            "Roadmap publication could not create the draft pull request.",
            cancellationToken).ConfigureAwait(false);
        var headSha = RequireSha(RequireObject(document.RootElement, "head"), "sha");
        var baseSha = RequireSha(RequireObject(document.RootElement, "base"), "sha");
        if (!RequireBoolean(document.RootElement, "draft")
            || !string.Equals(headSha, commitSha, StringComparison.Ordinal)
            || !string.Equals(baseSha, plan.BaseSha, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Roadmap publication pull request does not match the approved base and head.");
        }

        var url = RequireString(document.RootElement, "html_url");
        if (!IsExpectedPullRequestUrl(url, plan.Repository))
        {
            throw new InvalidOperationException("Roadmap publication did not return a pull request URL.");
        }

        return url;
    }

    private async Task<JsonDocument> Send(
        HttpMethod method,
        Uri endpoint,
        Action<Utf8JsonWriter>? writeBody,
        HttpStatusCode expectedStatus,
        string failureMessage,
        CancellationToken cancellationToken,
        string? branchCollisionMessage = null)
    {
        using var request = new HttpRequestMessage(method, endpoint);
        request.Headers.Add("X-GitHub-Api-Version", ApiVersion);
        if (writeBody is not null)
        {
            request.Content = CreateJsonContent(writeBody);
        }

        using var timeout = new CancellationTokenSource(GitHubRequestTimeout.Duration, _timeProvider);
        using var requestCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);
        HttpResponseMessage response;
        try
        {
            response = await _httpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                requestCancellation.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw new OperationCanceledException(cancellationToken);
        }
        catch (OperationCanceledException) when (timeout.IsCancellationRequested)
        {
            throw GitHubRequestTimeout.Create("roadmap publication");
        }
        catch (HttpRequestException)
        {
            throw new InvalidOperationException(failureMessage);
        }

        using (response)
        {
            if (response.StatusCode != expectedStatus)
            {
                if (response.StatusCode == HttpStatusCode.UnprocessableEntity
                    && branchCollisionMessage is not null)
                {
                    throw new InvalidOperationException(branchCollisionMessage);
                }

                throw new InvalidOperationException(failureMessage);
            }

            try
            {
                var content = await BoundedHttpContentReader.Read(
                    response.Content,
                    MaxResponseBytes,
                    "Roadmap publication response exceeded the allowed size.",
                    requestCancellation.Token).ConfigureAwait(false);
                return JsonDocument.Parse(content);
            }
            catch (JsonException)
            {
                throw new InvalidOperationException(failureMessage);
            }
        }
    }

    private static ByteArrayContent CreateJsonContent(Action<Utf8JsonWriter> writeBody)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            writeBody(writer);
            writer.WriteEndObject();
        }

        var content = new ByteArrayContent(stream.ToArray());
        content.Headers.ContentType = new MediaTypeHeaderValue("application/json")
        {
            CharSet = Encoding.UTF8.WebName
        };
        return content;
    }

    private static void AddFileEntry(
        Dictionary<string, PublicationFileEntry> entries,
        PublicationFileEntry entry)
    {
        if (!entries.TryAdd(entry.Path, entry))
        {
            throw new InvalidOperationException("Roadmap publication contains duplicate changed paths.");
        }
    }

    private static Uri CreateRepositoryEndpoint(string repository)
    {
        var parts = repository.Split('/');
        return new Uri(
            $"https://api.github.com/repos/{Uri.EscapeDataString(parts[0])}/{Uri.EscapeDataString(parts[1])}/",
            UriKind.Absolute);
    }

    private static void RequireDefaultOid(string expected, string actual, string message)
    {
        if (!string.Equals(expected, actual, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(message);
        }
    }

    private static JsonElement RequireObject(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property)
            || property.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidOperationException("Roadmap publication response did not contain the expected metadata.");
        }

        return property;
    }

    private static JsonElement RequireArray(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property)
            || property.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidOperationException("Roadmap publication response did not contain the expected metadata.");
        }

        return property;
    }

    private static bool RequireBoolean(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property)
            || property.ValueKind is not JsonValueKind.True and not JsonValueKind.False)
        {
            throw new InvalidOperationException("Roadmap publication response did not contain the expected metadata.");
        }

        return property.GetBoolean();
    }

    private static string RequireSha(JsonElement element, string propertyName)
    {
        var value = RequireString(element, propertyName);
        if (value.Length != 40
            || value.Any(character => character is not (>= '0' and <= '9' or >= 'a' and <= 'f')))
        {
            throw new InvalidOperationException("Roadmap publication response did not contain a valid Git SHA.");
        }

        return value;
    }

    private static string RequireString(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property)
            || property.ValueKind != JsonValueKind.String)
        {
            throw new InvalidOperationException("Roadmap publication response did not contain the expected metadata.");
        }

        return property.GetString() ?? string.Empty;
    }

    private static bool IsExpectedPullRequestUrl(string value, string repository)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri)
            || !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.Ordinal)
            || !string.Equals(uri.Host, "github.com", StringComparison.OrdinalIgnoreCase)
            || !string.IsNullOrEmpty(uri.Query)
            || !string.IsNullOrEmpty(uri.Fragment))
        {
            return false;
        }

        var prefix = $"/{repository}/pull/";
        return uri.AbsolutePath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            && int.TryParse(uri.AbsolutePath[prefix.Length..], out var number)
            && number > 0;
    }

    private sealed record PublicationFileEntry(string Path, string Mode, byte[]? Content);

    private sealed record PublicationTreeEntry(string Path, string Mode, string? Sha);
}
