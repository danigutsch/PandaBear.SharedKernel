namespace PandaBear.RepoConfig.Tool;

/// <summary>GraphQL response that lists the field values of a project item.</summary>
/// <param name="Data">Response payload.</param>
/// <param name="Errors">GraphQL errors, if any.</param>
internal sealed record GitHubProjectItemResponse(GitHubProjectItemResponse.ItemPayload? Data, IReadOnlyList<GitHubProjectResponse.Error>? Errors)
{
    /// <summary>Payload of an item field-values query.</summary>
    /// <param name="Node">Queried project item.</param>
    internal sealed record ItemPayload(Item? Node);
    /// <summary>Project item with its field values.</summary>
    /// <param name="FieldValues">Field values of the item.</param>
    internal sealed record Item(FieldValueConnection? FieldValues);
    /// <summary>List of project field values.</summary>
    /// <param name="Nodes">Field values.</param>
    internal sealed record FieldValueConnection(IReadOnlyList<FieldValue>? Nodes);
    /// <summary>Value of one project field; at most one of the typed values is set.</summary>
    /// <param name="Number">Number value.</param>
    /// <param name="Text">Text value.</param>
    /// <param name="OptionId">Selected option ID of a single-select field.</param>
    /// <param name="Field">Field the value belongs to.</param>
    internal sealed record FieldValue(decimal? Number, string? Text, string? OptionId, Field? Field);
    /// <summary>Reference to a project field.</summary>
    /// <param name="Id">Field ID.</param>
    /// <param name="Name">Field name.</param>
    internal sealed record Field(string? Id, string? Name);
}
