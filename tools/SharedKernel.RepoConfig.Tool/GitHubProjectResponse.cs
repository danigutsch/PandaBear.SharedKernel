namespace SharedKernel.RepoConfig.Tool;

internal sealed record GitHubProjectResponse(GitHubProjectResponse.Payload? Data, IReadOnlyList<GitHubProjectResponse.Error>? Errors)
{
    internal sealed record Payload(Node? Node, Repository? Repository, AddProjectItemResult? AddProjectV2ItemById, UpdateProjectFieldResult? UpdateProjectV2ItemFieldValue);
    internal sealed record Node(string? Id, int? Number, Owner? Owner, ProjectItemConnection? Items, ProjectFieldConnection? Fields);
    internal sealed record Repository(Content? Issue, Content? PullRequest);
    internal sealed record Project(string? Id, int? Number, Owner? Owner, ProjectItemConnection? Items);
    internal sealed record Owner(string? Login);
    internal sealed record Content(string? Id);
    internal sealed record AddProjectItemResult(ProjectItem? Item);
    internal sealed record UpdateProjectFieldResult(ProjectItem? ProjectV2Item);
    internal sealed record ProjectItem(string? Id);
    internal sealed record ProjectItemConnection(IReadOnlyList<ProjectItemNode>? Nodes, PageInfo? PageInfo);
    internal sealed record ProjectItemNode(string? Id, Content? Content);
    internal sealed record PageInfo(bool HasNextPage, string? EndCursor);
    internal sealed record ProjectFieldConnection(IReadOnlyList<ProjectField>? Nodes);
    internal sealed record ProjectField(string? Id, string? Name, string? DataType, IReadOnlyList<ProjectFieldOption>? Options);
    internal sealed record ProjectFieldOption(string? Id, string? Name);
    internal sealed record Error(string? Type);
}
