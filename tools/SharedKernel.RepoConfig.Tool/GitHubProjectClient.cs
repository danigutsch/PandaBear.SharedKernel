using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace PandaBear.RepoConfig.Tool;

/// <summary>GraphQL client for the GitHub Projects v2 API.</summary>
/// <param name="httpClient">Authenticated GitHub client.</param>
internal sealed class GitHubProjectClient(HttpClient httpClient)
{
    // One client serves one sync run, so field definitions and item membership are read once per target and reused.
    private string? _cachedProjectId;
    private IReadOnlyList<GitHubProjectResponse.ProjectField>? _fields;
    private Dictionary<string, string>? _itemIdsByIssueId;

    /// <summary>Verifies that the project target exists with the expected owner and number.</summary>
    /// <param name="target">Project to verify.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <exception cref="InvalidOperationException">The project target does not exist with the expected owner and number.</exception>
    public async Task VerifyTarget(GitHubProjectTarget target, CancellationToken cancellationToken)
    {
        const string Query = "query($id:ID!){node(id:$id){... on ProjectV2{id number owner{... on User{login} ... on Organization{login}}}}}";
        var response = await Send(Query, writer =>
        {
            writer.WriteString("id", target.Id);
        }, GitHubProjectJsonContext.Default.ProjectResponse, response => response.Errors, cancellationToken).ConfigureAwait(false);
        var project = response.Data?.Node;
        if (!string.Equals(project?.Id, target.Id, StringComparison.Ordinal)
            || project?.Number != target.Number
            || !string.Equals(project.Owner?.Login, target.Owner, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Configured GitHub Project target could not be verified.");
        }
    }

    /// <summary>Returns the GraphQL node ID of an issue.</summary>
    /// <param name="repository">Repository in <c>owner/name</c> form.</param>
    /// <param name="issueNumber">Issue number.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <exception cref="InvalidOperationException">GitHub does not return a node ID for the issue.</exception>
    public async Task<string> GetIssueNodeId(string repository, int issueNumber, CancellationToken cancellationToken)
    {
        var parts = repository.Split('/');
        const string Query = "query($owner:String!,$repo:String!,$number:Int!){repository(owner:$owner,name:$repo){issue(number:$number){id}}}";
        var response = await Send(Query, writer =>
        {
            writer.WriteString("owner", parts[0]);
            writer.WriteString("repo", parts[1]);
            writer.WriteNumber("number", issueNumber);
        }, GitHubProjectJsonContext.Default.ProjectResponse, response => response.Errors, cancellationToken).ConfigureAwait(false);
        var repositoryResult = response.Data?.Repository;
        var issueId = repositoryResult?.Issue?.Id;
        return string.IsNullOrWhiteSpace(issueId)
            ? throw new InvalidOperationException($"GitHub issue mapping could not be found: #{issueNumber}.")
            : issueId;
    }

    /// <summary>Adds the issue to the project and returns the project item ID.</summary>
    /// <param name="target">Project to add to.</param>
    /// <param name="issueId">Issue node ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <exception cref="InvalidOperationException">GitHub does not add the issue to the project.</exception>
    public async Task<string> AddIssue(GitHubProjectTarget target, string issueId, CancellationToken cancellationToken)
    {
        const string Query = "mutation($projectId:ID!,$contentId:ID!){addProjectV2ItemById(input:{projectId:$projectId,contentId:$contentId}){item{id}}}";
        var response = await Send(Query, writer =>
        {
            writer.WriteString("projectId", target.Id);
            writer.WriteString("contentId", issueId);
        }, GitHubProjectJsonContext.Default.ProjectResponse, response => response.Errors, cancellationToken).ConfigureAwait(false);
        var itemId = response.Data?.AddProjectV2ItemById?.Item?.Id;
        return string.IsNullOrWhiteSpace(itemId)
            ? throw new InvalidOperationException("GitHub Project item could not be added.")
            : RememberItem(target, issueId, itemId);
    }

    /// <summary>Returns the project item ID of the issue, or <see langword="null" /> when the project does not contain it.</summary>
    /// <param name="target">Project to search.</param>
    /// <param name="issueId">Issue node ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task<string?> FindItemId(GitHubProjectTarget target, string issueId, CancellationToken cancellationToken)
    {
        ResetCacheForOtherProject(target);
        _itemIdsByIssueId ??= await ReadItemIds(target, cancellationToken).ConfigureAwait(false);
        return _itemIdsByIssueId.TryGetValue(issueId, out var itemId) ? itemId : null;
    }

    private async Task<Dictionary<string, string>> ReadItemIds(GitHubProjectTarget target, CancellationToken cancellationToken)
    {
        const string Query = "query($id:ID!,$after:String){node(id:$id){... on ProjectV2{items(first:100,after:$after){nodes{id content{... on Issue{id}}}pageInfo{hasNextPage endCursor}}}}}";
        Dictionary<string, string> itemIds = new(StringComparer.Ordinal);
        string? cursor = null;
        GitHubProjectResponse.ProjectItemConnection items;
        do
        {
            var response = await Send(Query, writer =>
            {
                writer.WriteString("id", target.Id);
                if (cursor is not null)
                {
                    writer.WriteString("after", cursor);
                }
                else
                {
                    writer.WriteNull("after");
                }
            }, GitHubProjectJsonContext.Default.ProjectResponse, response => response.Errors, cancellationToken).ConfigureAwait(false);
            items = response.Data?.Node?.Items
                ?? throw new InvalidOperationException("GitHub Project items could not be read.");
            foreach (var item in items.Nodes ?? [])
            {
                if (!string.IsNullOrWhiteSpace(item.Id) && !string.IsNullOrWhiteSpace(item.Content?.Id))
                {
                    itemIds.TryAdd(item.Content.Id, item.Id);
                }
            }

            cursor = items.PageInfo?.EndCursor;
        }
        while (items.PageInfo?.HasNextPage == true);

        return itemIds;
    }

    private string RememberItem(GitHubProjectTarget target, string issueId, string itemId)
    {
        ResetCacheForOtherProject(target);
        _itemIdsByIssueId?[issueId] = itemId;
        return itemId;
    }

    private void ResetCacheForOtherProject(GitHubProjectTarget target)
    {
        if (string.Equals(_cachedProjectId, target.Id, StringComparison.Ordinal))
        {
            return;
        }

        _cachedProjectId = target.Id;
        _fields = null;
        _itemIdsByIssueId = null;
    }

    /// <summary>Returns the fields of the project.</summary>
    /// <param name="target">Project to read.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <exception cref="InvalidOperationException">GitHub does not return the project fields.</exception>
    public async Task<IReadOnlyList<GitHubProjectResponse.ProjectField>> GetFields(GitHubProjectTarget target, CancellationToken cancellationToken)
    {
        ResetCacheForOtherProject(target);
        if (_fields is not null)
        {
            return _fields;
        }

        const string Query = "query($id:ID!){node(id:$id){... on ProjectV2{fields(first:100){nodes{__typename ... on ProjectV2FieldCommon{id name dataType} ... on ProjectV2SingleSelectField{options{id name}}}}}}}";
        var response = await Send(Query, writer => writer.WriteString("id", target.Id), GitHubProjectJsonContext.Default.ProjectResponse, result => result.Errors, cancellationToken).ConfigureAwait(false);
        _fields = response.Data?.Node?.Fields?.Nodes
            ?? throw new InvalidOperationException("GitHub Project fields could not be read.");
        return _fields;
    }

    /// <summary>Returns the field values of the project item.</summary>
    /// <param name="itemId">Project item ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <exception cref="InvalidOperationException">GitHub does not return the project item field values.</exception>
    public async Task<IReadOnlyList<GitHubProjectItemResponse.FieldValue>> GetFieldValues(string itemId, CancellationToken cancellationToken)
    {
        const string Query = "query($id:ID!){node(id:$id){... on ProjectV2Item{fieldValues(first:100){nodes{... on ProjectV2ItemFieldNumberValue{number field{... on ProjectV2FieldCommon{id name}}} ... on ProjectV2ItemFieldTextValue{text field{... on ProjectV2FieldCommon{id name}}} ... on ProjectV2ItemFieldSingleSelectValue{optionId field{... on ProjectV2FieldCommon{id name}}}}}}}}";
        var response = await Send(Query, writer => writer.WriteString("id", itemId), GitHubProjectJsonContext.Default.ProjectItemResponse, result => result.Errors, cancellationToken).ConfigureAwait(false);
        return response.Data?.Node?.FieldValues?.Nodes
            ?? throw new InvalidOperationException("GitHub Project field values could not be read.");
    }

    /// <summary>Sets a number field of the project item.</summary>
    /// <param name="target">Project containing the item.</param>
    /// <param name="itemId">Project item ID.</param>
    /// <param name="fieldId">Field ID.</param>
    /// <param name="value">New number.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task UpdateNumber(GitHubProjectTarget target, string itemId, string fieldId, decimal value, CancellationToken cancellationToken)
    {
        const string Query = "mutation($projectId:ID!,$itemId:ID!,$fieldId:ID!,$value:Float!){updateProjectV2ItemFieldValue(input:{projectId:$projectId,itemId:$itemId,fieldId:$fieldId,value:{number:$value}}){projectV2Item{id}}}";
        await UpdateField(Query, target, itemId, fieldId, writer => writer.WriteNumber("value", value), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Sets a text field of the project item.</summary>
    /// <param name="target">Project containing the item.</param>
    /// <param name="itemId">Project item ID.</param>
    /// <param name="fieldId">Field ID.</param>
    /// <param name="value">New text.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task UpdateText(GitHubProjectTarget target, string itemId, string fieldId, string value, CancellationToken cancellationToken)
    {
        const string Query = "mutation($projectId:ID!,$itemId:ID!,$fieldId:ID!,$value:String!){updateProjectV2ItemFieldValue(input:{projectId:$projectId,itemId:$itemId,fieldId:$fieldId,value:{text:$value}}){projectV2Item{id}}}";
        await UpdateField(Query, target, itemId, fieldId, writer => writer.WriteString("value", value), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Sets a single-select field of the project item.</summary>
    /// <param name="target">Project containing the item.</param>
    /// <param name="itemId">Project item ID.</param>
    /// <param name="fieldId">Field ID.</param>
    /// <param name="optionId">ID of the option to select.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task UpdateSingleSelect(GitHubProjectTarget target, string itemId, string fieldId, string optionId, CancellationToken cancellationToken)
    {
        const string Query = "mutation($projectId:ID!,$itemId:ID!,$fieldId:ID!,$optionId:String!){updateProjectV2ItemFieldValue(input:{projectId:$projectId,itemId:$itemId,fieldId:$fieldId,value:{singleSelectOptionId:$optionId}}){projectV2Item{id}}}";
        await UpdateField(Query, target, itemId, fieldId, writer => writer.WriteString("optionId", optionId), cancellationToken).ConfigureAwait(false);
    }

    private async Task UpdateField(string query, GitHubProjectTarget target, string itemId, string fieldId, Action<Utf8JsonWriter> writeValue, CancellationToken cancellationToken)
    {
        var response = await Send(query, writer =>
        {
            writer.WriteString("projectId", target.Id);
            writer.WriteString("itemId", itemId);
            writer.WriteString("fieldId", fieldId);
            writeValue(writer);
        }, GitHubProjectJsonContext.Default.ProjectResponse, result => result.Errors, cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(response.Data?.UpdateProjectV2ItemFieldValue?.ProjectV2Item?.Id))
        {
            throw new InvalidOperationException("GitHub Project field could not be updated.");
        }
    }

    private async Task<TResponse> Send<TResponse>(string query, Action<Utf8JsonWriter> writeVariables, JsonTypeInfo<TResponse> typeInfo, Func<TResponse, IReadOnlyList<GitHubProjectResponse.Error>?> getErrors, CancellationToken cancellationToken)
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
        using var response = await httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"GitHub Project request failed with HTTP {(int)response.StatusCode}.");
        }

        var content = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
        var result = JsonSerializer.Deserialize(content, typeInfo)
            ?? throw new InvalidOperationException("GitHub Project request returned no result.");
        var errors = getErrors(result);
        if (errors?.Count > 0)
        {
            var errorType = errors.Select(error => error.Type).FirstOrDefault(type => !string.IsNullOrWhiteSpace(type));
            throw new InvalidOperationException(string.IsNullOrWhiteSpace(errorType)
                ? "GitHub Project request failed."
                : $"GitHub Project request failed ({errorType}).");
        }

        return result;
    }

}
