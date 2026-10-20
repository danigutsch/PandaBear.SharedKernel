namespace SharedKernel.RepoConfig.Tool;

internal sealed record GitHubProjectItemResponse(GitHubProjectItemResponse.ItemPayload? Data, IReadOnlyList<GitHubProjectResponse.Error>? Errors)
{
    internal sealed record ItemPayload(Item? Node);
    internal sealed record Item(FieldValueConnection? FieldValues);
    internal sealed record FieldValueConnection(IReadOnlyList<FieldValue>? Nodes);
    internal sealed record FieldValue(decimal? Number, string? Text, string? OptionId, Field? Field);
    internal sealed record Field(string? Id, string? Name);
}
