using System.Net;
using System.Text;
using System.Text.Json;

namespace SharedKernel.RepoConfig.Tool;

internal sealed class GitHubRoadmapReconcileClient(HttpClient httpClient)
{
    private const string ReconcileQuery = """
        query($owner:String!,$name:String!,$after:String){
          repository(owner:$owner,name:$name){
            defaultBranchRef{target{oid}}
            issues(first:100,after:$after,states:[OPEN],orderBy:{field:CREATED_AT,direction:ASC}){
              nodes{
                __typename
                number
                title
                state
                labels(first:100){nodes{name}pageInfo{hasNextPage endCursor}}
                parent{number state repository{nameWithOwner}}
                subIssues(first:100){nodes{number state repository{nameWithOwner}}pageInfo{hasNextPage endCursor}}
                blockedBy(first:100){nodes{number state repository{nameWithOwner}}pageInfo{hasNextPage endCursor}}
                blocking(first:100){nodes{number state repository{nameWithOwner}}pageInfo{hasNextPage endCursor}}
              }
              pageInfo{hasNextPage endCursor}
            }
          }
        }
        """;

    private const string IssueQuery = "query($owner:String!,$name:String!,$number:Int!){repository(owner:$owner,name:$name){issueOrPullRequest(number:$number){__typename ... on Issue{number title state labels(first:100){nodes{name}pageInfo{hasNextPage endCursor}} parent{number state repository{nameWithOwner}} subIssues(first:100){nodes{number state repository{nameWithOwner}}pageInfo{hasNextPage endCursor}} blockedBy(first:100){nodes{number state repository{nameWithOwner}}pageInfo{hasNextPage endCursor}} blocking(first:100){nodes{number state repository{nameWithOwner}}pageInfo{hasNextPage endCursor}}}}}}";

    public async Task<GitHubRoadmapReconcileSnapshot> ReadSnapshot(
        string repository,
        int[] requiredIssueNumbers,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(requiredIssueNumbers);

        var (owner, name) = SplitRepository(repository);
        var (repositoryCommit, issuesByNumber) = await ReadOpenIssues(owner, name, cancellationToken).ConfigureAwait(false);
        var supplementalIssueNumbers = DiscoverSupplementalIssueNumbers(requiredIssueNumbers, issuesByNumber.Values, repository);
        await AddSupplementalIssues(owner, name, supplementalIssueNumbers, issuesByNumber, cancellationToken).ConfigureAwait(false);

        return new GitHubRoadmapReconcileSnapshot(repositoryCommit, issuesByNumber.Values.OrderBy(issue => issue.Number).ToArray());
    }

    private async Task<(string? RepositoryCommit, Dictionary<int, GitHubRoadmapReconcileIssue> IssuesByNumber)> ReadOpenIssues(
        string owner,
        string name,
        CancellationToken cancellationToken)
    {
        Dictionary<int, GitHubRoadmapReconcileIssue> issuesByNumber = [];
        string? repositoryCommit = null;
        string? cursor = null;
        while (true)
        {
            var page = await ReadOpenIssuePage(
                owner,
                name,
                cursor,
                readRepositoryCommit: repositoryCommit is null,
                issuesByNumber,
                cancellationToken).ConfigureAwait(false);
            repositoryCommit ??= page.RepositoryCommit;
            if (!page.HasNextPage)
            {
                return (repositoryCommit, issuesByNumber);
            }

            cursor = RequireNextCursor(page.EndCursor);
        }
    }

    private async Task<OpenIssuePage> ReadOpenIssuePage(
        string owner,
        string name,
        string? cursor,
        bool readRepositoryCommit,
        Dictionary<int, GitHubRoadmapReconcileIssue> issuesByNumber,
        CancellationToken cancellationToken)
    {
        using var document = await Send(
            ReconcileQuery,
            writer => WriteReconcileVariables(writer, owner, name, cursor),
            cancellationToken).ConfigureAwait(false);
        var repositoryElement = GetRepository(document.RootElement);
        var repositoryCommit = readRepositoryCommit ? ReadRepositoryCommit(repositoryElement) : null;
        var connection = GetRequiredObject(repositoryElement, "issues");
        foreach (var node in GetRequiredArray(connection, "nodes").EnumerateArray())
        {
            var issue = await ReadIssue(node, owner, name, cancellationToken).ConfigureAwait(false);
            AddOpenIssue(issuesByNumber, issue);
        }

        var pageInfo = GetRequiredObject(connection, "pageInfo");
        return new OpenIssuePage(
            repositoryCommit,
            GetRequiredBoolean(pageInfo, "hasNextPage"),
            GetNullableString(pageInfo, "endCursor"));
    }

    private static void WriteReconcileVariables(Utf8JsonWriter writer, string owner, string name, string? cursor)
    {
        writer.WriteString("owner", owner);
        writer.WriteString("name", name);
        if (cursor is null)
        {
            writer.WriteNull("after");
            return;
        }

        writer.WriteString("after", cursor);
    }

    private static void AddOpenIssue(
        Dictionary<int, GitHubRoadmapReconcileIssue> issuesByNumber,
        GitHubRoadmapReconcileIssue issue)
    {
        if (!issuesByNumber.TryAdd(issue.Number, issue))
        {
            throw new InvalidOperationException("GitHub reconciliation response contains duplicate issue numbers.");
        }
    }

    private static string RequireNextCursor(string? cursor)
    {
        if (string.IsNullOrWhiteSpace(cursor))
        {
            throw new InvalidOperationException("GitHub reconciliation response has an incomplete issue page.");
        }

        return cursor;
    }

    private static HashSet<int> DiscoverSupplementalIssueNumbers(
        IEnumerable<int> requiredIssueNumbers,
        IEnumerable<GitHubRoadmapReconcileIssue> issues,
        string repository)
    {
        HashSet<int> supplementalIssueNumbers = [.. requiredIssueNumbers];
        foreach (var issue in issues)
        {
            AddClosedRelation(issue.Parent, repository, supplementalIssueNumbers);
            foreach (var relation in issue.SubIssues.Concat(issue.BlockedBy).Concat(issue.Blocking))
            {
                AddClosedRelation(relation, repository, supplementalIssueNumbers);
            }
        }

        return supplementalIssueNumbers;
    }

    private async Task AddSupplementalIssues(
        string owner,
        string name,
        IEnumerable<int> supplementalIssueNumbers,
        Dictionary<int, GitHubRoadmapReconcileIssue> issuesByNumber,
        CancellationToken cancellationToken)
    {
        foreach (var issueNumber in supplementalIssueNumbers.Order())
        {
            if (issuesByNumber.ContainsKey(issueNumber))
            {
                continue;
            }

            var issue = await ReadIssueByNumber(owner, name, issueNumber, cancellationToken).ConfigureAwait(false);
            if (issue.Number != issueNumber || !issuesByNumber.TryAdd(issue.Number, issue))
            {
                throw new InvalidOperationException("GitHub reconciliation response contains inconsistent issue metadata.");
            }
        }
    }

    private sealed record OpenIssuePage(
        string? RepositoryCommit,
        bool HasNextPage,
        string? EndCursor);

    private async Task<GitHubRoadmapReconcileIssue> ReadIssueByNumber(
        string owner,
        string name,
        int issueNumber,
        CancellationToken cancellationToken)
    {
        using var document = await Send(
            IssueQuery,
            writer =>
            {
                writer.WriteString("owner", owner);
                writer.WriteString("name", name);
                writer.WriteNumber("number", issueNumber);
            },
            cancellationToken).ConfigureAwait(false);
        var node = GetRequiredObject(GetRepository(document.RootElement), "issueOrPullRequest");
        return await ReadIssue(node, owner, name, cancellationToken).ConfigureAwait(false);
    }

    private static void AddClosedRelation(
        GitHubRoadmapReconcileRelation? relation,
        string repository,
        HashSet<int> issueNumbers)
    {
        if (relation is not null
            && string.Equals(relation.Repository, repository, StringComparison.OrdinalIgnoreCase)
            && string.Equals(relation.State, "CLOSED", StringComparison.Ordinal))
        {
            issueNumbers.Add(relation.Number);
        }
    }

    private async Task<JsonDocument> Send(string query, Action<Utf8JsonWriter> writeVariables, CancellationToken cancellationToken)
    {
        using var timeout = new CancellationTokenSource(GitHubRequestTimeout.Duration);
        using var requestCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);
        try
        {
            return await SendRequest(query, writeVariables, requestCancellation.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw new OperationCanceledException(cancellationToken);
        }
        catch (OperationCanceledException) when (timeout.IsCancellationRequested)
        {
            throw GitHubRequestTimeout.Create("reconciliation");
        }
    }

    private async Task<JsonDocument> SendRequest(string query, Action<Utf8JsonWriter> writeVariables, CancellationToken cancellationToken)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            writer.WriteString("query", query);
            writer.WritePropertyName("variables");
            writer.WriteStartObject();
            writeVariables(writer);
            writer.WriteEndObject();
            writer.WriteEndObject();
        }

        var endpoint = new UriBuilder(Uri.UriSchemeHttps, "api.github.com") { Path = "graphql" }.Uri;
        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = new StringContent(Encoding.UTF8.GetString(stream.ToArray()), Encoding.UTF8, "application/json")
        };

        HttpResponseMessage response;
        try
        {
            response = await httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        }
        catch (HttpRequestException exception)
        {
            throw new InvalidOperationException("GitHub reconciliation request failed.", exception);
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                throw CreateGitHubFailure(response.StatusCode);
            }

            JsonDocument document;
            try
            {
                using var responseStream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
                document = await JsonDocument.ParseAsync(responseStream, cancellationToken: cancellationToken).ConfigureAwait(false);
            }
            catch (JsonException exception)
            {
                throw new InvalidOperationException("GitHub reconciliation response could not be read.", exception);
            }

            if (HasErrors(document.RootElement))
            {
                document.Dispose();
                throw new InvalidOperationException("GitHub reconciliation request failed.");
            }

            return document;
        }
    }

    private async Task<GitHubRoadmapReconcileIssue> ReadIssue(
        JsonElement root,
        string owner,
        string name,
        CancellationToken cancellationToken)
    {
        var typename = GetRequiredString(root, "__typename");
        if (!string.Equals(typename, "Issue", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("GitHub reconciliation rejected a pull request or unsupported node.");
        }

        var number = GetRequiredPositiveInteger(root, "number");
        var labels = await ReadAllLabels(root, owner, name, number, cancellationToken).ConfigureAwait(false);
        var subIssues = await ReadAllRelations(root, "subIssues", owner, name, number, cancellationToken).ConfigureAwait(false);
        var blockedBy = await ReadAllRelations(root, "blockedBy", owner, name, number, cancellationToken).ConfigureAwait(false);
        var blocking = await ReadAllRelations(root, "blocking", owner, name, number, cancellationToken).ConfigureAwait(false);
        return new GitHubRoadmapReconcileIssue(
            number,
            GetRequiredString(root, "title"),
            GetRequiredIssueState(root, "state"),
            labels,
            ReadNullableRelation(root, "parent"),
            subIssues,
            blockedBy,
            blocking);
    }

    private async Task<string[]> ReadAllLabels(
        JsonElement root,
        string owner,
        string name,
        int issueNumber,
        CancellationToken cancellationToken)
    {
        var labels = GetRequiredObject(root, "labels");
        HashSet<string> values = new(StringComparer.Ordinal);
        AddLabels(labels, values);
        var (hasNextPage, cursor) = ReadPageInfo(labels);
        while (hasNextPage)
        {
            using var document = await ReadConnectionPage(owner, name, issueNumber, "labels", cursor, cancellationToken).ConfigureAwait(false);
            var connection = GetIssueConnection(document.RootElement, "labels");
            AddLabels(connection, values);
            (hasNextPage, cursor) = ReadPageInfo(connection);
        }

        return values.Order(StringComparer.Ordinal).ToArray();
    }

    private static void AddLabels(JsonElement connection, HashSet<string> values)
    {
        foreach (var node in GetRequiredArray(connection, "nodes").EnumerateArray())
        {
            var label = GetRequiredString(node, "name");
            if (!string.Equals(label, label.Trim(), StringComparison.Ordinal) || !values.Add(label))
            {
                throw new InvalidOperationException("GitHub reconciliation response contains invalid labels.");
            }
        }
    }

    private static GitHubRoadmapReconcileRelation? ReadNullableRelation(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var relation) || relation.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        return ReadRelation(relation);
    }

    private async Task<GitHubRoadmapReconcileRelation[]> ReadAllRelations(
        JsonElement root,
        string propertyName,
        string owner,
        string name,
        int issueNumber,
        CancellationToken cancellationToken)
    {
        var connection = GetRequiredObject(root, propertyName);
        Dictionary<(string Repository, int Number), GitHubRoadmapReconcileRelation> relations = [];
        AddRelations(connection, relations);
        var (hasNextPage, cursor) = ReadPageInfo(connection);
        while (hasNextPage)
        {
            using var document = await ReadConnectionPage(owner, name, issueNumber, propertyName, cursor, cancellationToken).ConfigureAwait(false);
            connection = GetIssueConnection(document.RootElement, propertyName);
            AddRelations(connection, relations);
            (hasNextPage, cursor) = ReadPageInfo(connection);
        }

        return relations.Values
            .OrderBy(relation => relation.Repository, StringComparer.OrdinalIgnoreCase)
            .ThenBy(relation => relation.Number)
            .ToArray();
    }

    private static void AddRelations(
        JsonElement connection,
        Dictionary<(string Repository, int Number), GitHubRoadmapReconcileRelation> relations)
    {
        foreach (var node in GetRequiredArray(connection, "nodes").EnumerateArray())
        {
            var relation = ReadRelation(node);
            if (!relations.TryAdd((relation.Repository.ToUpperInvariant(), relation.Number), relation))
            {
                throw new InvalidOperationException("GitHub reconciliation response contains duplicate relationship metadata.");
            }
        }
    }

    private static GitHubRoadmapReconcileRelation ReadRelation(JsonElement root)
    {
        var repository = GetRequiredObject(root, "repository");
        return new GitHubRoadmapReconcileRelation(
            GetRequiredPositiveInteger(root, "number"),
            GetRequiredString(repository, "nameWithOwner"),
            GetRequiredIssueState(root, "state"));
    }

    private async Task<JsonDocument> ReadConnectionPage(
        string owner,
        string name,
        int issueNumber,
        string connectionName,
        string? cursor,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(cursor))
        {
            throw new InvalidOperationException($"GitHub reconciliation response has incomplete {connectionName} metadata.");
        }

        var query = CreateConnectionQuery(connectionName);
        return await Send(
            query,
            writer =>
            {
                writer.WriteString("owner", owner);
                writer.WriteString("name", name);
                writer.WriteNumber("number", issueNumber);
                writer.WriteString("after", cursor);
            },
            cancellationToken).ConfigureAwait(false);
    }

    private static string CreateConnectionQuery(string connectionName)
    {
        var selection = connectionName switch
        {
            "labels" => "nodes{name}",
            "subIssues" or "blockedBy" or "blocking" => "nodes{number state repository{nameWithOwner}}",
            _ => throw new InvalidOperationException("GitHub reconciliation requested an unsupported issue connection.")
        };
        return $"query($owner:String!,$name:String!,$number:Int!,$after:String!){{repository(owner:$owner,name:$name){{issue(number:$number){{{connectionName}(first:100,after:$after){{{selection}pageInfo{{hasNextPage endCursor}}}}}}}}}}";
    }

    private static JsonElement GetIssueConnection(JsonElement root, string connectionName)
    {
        var repository = GetRepository(root);
        var issue = GetRequiredObject(repository, "issue");
        return GetRequiredObject(issue, connectionName);
    }

    private static (bool HasNextPage, string? Cursor) ReadPageInfo(JsonElement connection)
    {
        var pageInfo = GetRequiredObject(connection, "pageInfo");
        var hasNextPage = GetRequiredBoolean(pageInfo, "hasNextPage");
        var cursor = GetNullableString(pageInfo, "endCursor");
        if (hasNextPage && string.IsNullOrWhiteSpace(cursor))
        {
            throw new InvalidOperationException("GitHub reconciliation response has an incomplete connection page.");
        }

        return (hasNextPage, cursor);
    }

    private static string? ReadRepositoryCommit(JsonElement repository)
    {
        if (!repository.TryGetProperty("defaultBranchRef", out var branch) || branch.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        return GetRequiredString(GetRequiredObject(branch, "target"), "oid");
    }

    private static JsonElement GetRepository(JsonElement root) => GetRequiredObject(GetRequiredObject(root, "data"), "repository");

    private static bool HasErrors(JsonElement root) => root.TryGetProperty("errors", out var errors)
        && errors.ValueKind == JsonValueKind.Array
        && errors.GetArrayLength() > 0;

    private static JsonElement GetRequiredObject(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidOperationException("GitHub reconciliation response did not contain the expected metadata.");
        }

        return property;
    }

    private static JsonElement GetRequiredArray(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidOperationException("GitHub reconciliation response did not contain the expected metadata.");
        }

        return property;
    }

    private static string GetRequiredString(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var property)
            || property.ValueKind != JsonValueKind.String
            || string.IsNullOrWhiteSpace(property.GetString()))
        {
            throw new InvalidOperationException("GitHub reconciliation response did not contain the expected metadata.");
        }

        return property.GetString() ?? string.Empty;
    }

    private static string? GetNullableString(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var property) || property.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        return property.ValueKind == JsonValueKind.String ? property.GetString() : null;
    }

    private static int GetRequiredPositiveInteger(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var property)
            || property.ValueKind != JsonValueKind.Number
            || !property.TryGetInt32(out var value)
            || value < 1)
        {
            throw new InvalidOperationException("GitHub reconciliation response did not contain the expected metadata.");
        }

        return value;
    }

    private static string GetRequiredIssueState(JsonElement root, string propertyName)
    {
        var state = GetRequiredString(root, propertyName);
        return state is "OPEN" or "CLOSED"
            ? state
            : throw new InvalidOperationException("GitHub reconciliation response contains an unsupported issue state.");
    }

    private static bool GetRequiredBoolean(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var property) || property.ValueKind is not JsonValueKind.True and not JsonValueKind.False)
        {
            throw new InvalidOperationException("GitHub reconciliation response did not contain the expected metadata.");
        }

        return property.GetBoolean();
    }

    private static InvalidOperationException CreateGitHubFailure(HttpStatusCode statusCode)
    {
        var hint = statusCode switch
        {
            HttpStatusCode.Unauthorized => " (authentication required)",
            HttpStatusCode.Forbidden => " (access denied or rate limited)",
            HttpStatusCode.NotFound => " (resource not found or inaccessible)",
            HttpStatusCode.TooManyRequests => " (rate limited)",
            _ => string.Empty
        };
        return new InvalidOperationException($"GitHub reconciliation request failed: HTTP {(int)statusCode}{hint}.");
    }

    private static (string Owner, string Name) SplitRepository(string repository)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repository);

        var parts = repository.Split('/');
        if (parts.Length != 2)
        {
            throw new InvalidOperationException("GitHub reconciliation requires a repository shaped as owner/repository.");
        }

        return (parts[0], parts[1]);
    }
}
