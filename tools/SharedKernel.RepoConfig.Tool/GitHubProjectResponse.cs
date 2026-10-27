namespace PandaBear.RepoConfig.Tool;

/// <summary>GraphQL response of a GitHub project query or mutation.</summary>
/// <param name="Data">Response payload.</param>
/// <param name="Errors">GraphQL errors, if any.</param>
internal sealed record GitHubProjectResponse(GitHubProjectResponse.Payload? Data, IReadOnlyList<GitHubProjectResponse.Error>? Errors)
{
    /// <summary>Payload of a project query or mutation; the member that is set depends on the operation.</summary>
    /// <param name="Node">Queried node.</param>
    /// <param name="Repository">Queried repository.</param>
    /// <param name="AddProjectV2ItemById">Result of adding an item to a project.</param>
    /// <param name="UpdateProjectV2ItemFieldValue">Result of updating an item field.</param>
    internal sealed record Payload(Node? Node, Repository? Repository, AddProjectItemResult? AddProjectV2ItemById, UpdateProjectFieldResult? UpdateProjectV2ItemFieldValue);
    /// <summary>Queried project node.</summary>
    /// <param name="Id">Node ID.</param>
    /// <param name="Number">Project number.</param>
    /// <param name="Owner">Owner of the project.</param>
    /// <param name="Items">Items of the project.</param>
    /// <param name="Fields">Fields of the project.</param>
    internal sealed record Node(string? Id, int? Number, Owner? Owner, ProjectItemConnection? Items, ProjectFieldConnection? Fields);
    /// <summary>Repository with an issue or pull request lookup.</summary>
    /// <param name="Issue">Issue content.</param>
    /// <param name="PullRequest">Pull request content.</param>
    internal sealed record Repository(Content? Issue, Content? PullRequest);
    /// <summary>Project summary.</summary>
    /// <param name="Id">Project node ID.</param>
    /// <param name="Number">Project number.</param>
    /// <param name="Owner">Owner of the project.</param>
    /// <param name="Items">Items of the project.</param>
    internal sealed record Project(string? Id, int? Number, Owner? Owner, ProjectItemConnection? Items);
    /// <summary>Owner of a project.</summary>
    /// <param name="Login">Login name of the owner.</param>
    internal sealed record Owner(string? Login);
    /// <summary>Content of an issue or pull request.</summary>
    /// <param name="Id">Content node ID.</param>
    internal sealed record Content(string? Id);
    /// <summary>Result of adding an item to a project.</summary>
    /// <param name="Item">Created project item.</param>
    internal sealed record AddProjectItemResult(ProjectItem? Item);
    /// <summary>Result of updating a project item field.</summary>
    /// <param name="ProjectV2Item">Updated project item.</param>
    internal sealed record UpdateProjectFieldResult(ProjectItem? ProjectV2Item);
    /// <summary>Project item reference.</summary>
    /// <param name="Id">Project item ID.</param>
    internal sealed record ProjectItem(string? Id);
    /// <summary>Page of project items.</summary>
    /// <param name="Nodes">Items on the page.</param>
    /// <param name="PageInfo">Paging information.</param>
    internal sealed record ProjectItemConnection(IReadOnlyList<ProjectItemNode>? Nodes, PageInfo? PageInfo);
    /// <summary>Project item with its linked content.</summary>
    /// <param name="Id">Project item ID.</param>
    /// <param name="Content">Issue or pull request the item wraps.</param>
    internal sealed record ProjectItemNode(string? Id, Content? Content);
    /// <summary>Paging information of a connection.</summary>
    /// <param name="HasNextPage">Whether more pages exist.</param>
    /// <param name="EndCursor">Cursor to request the next page.</param>
    internal sealed record PageInfo(bool HasNextPage, string? EndCursor);
    /// <summary>List of project fields.</summary>
    /// <param name="Nodes">Fields.</param>
    internal sealed record ProjectFieldConnection(IReadOnlyList<ProjectField>? Nodes);
    /// <summary>Project field definition.</summary>
    /// <param name="Id">Field ID.</param>
    /// <param name="Name">Field name.</param>
    /// <param name="DataType">Field data type name.</param>
    /// <param name="Options">Options of a single-select field.</param>
    internal sealed record ProjectField(string? Id, string? Name, string? DataType, IReadOnlyList<ProjectFieldOption>? Options);
    /// <summary>Option of a single-select project field.</summary>
    /// <param name="Id">Option ID.</param>
    /// <param name="Name">Option name.</param>
    internal sealed record ProjectFieldOption(string? Id, string? Name);
    /// <summary>GraphQL error.</summary>
    /// <param name="Type">Error type.</param>
    internal sealed record Error(string? Type);
}
