namespace PandaBear.VaultManager.Tool.Merge;

/// <summary>User decision for one field of one group. It names a member, never a value.</summary>
/// <param name="FieldPath">Field path, one of <see cref="FieldPaths"/>.</param>
/// <param name="Kind">What the decision selects.</param>
/// <param name="OriginKey">Origin key of the chosen member for <see cref="FieldChoiceKind.Member"/>; otherwise <see langword="null"/>.</param>
internal sealed record FieldChoice(string FieldPath, FieldChoiceKind Kind, string? OriginKey = null)
{
    /// <summary>Chooses the value of one member.</summary>
    /// <param name="fieldPath">Field path.</param>
    /// <param name="originKey">Origin key of the chosen member.</param>
    /// <returns>The decision.</returns>
    public static FieldChoice Member(string fieldPath, string originKey) => new(fieldPath, FieldChoiceKind.Member, originKey);

    /// <summary>Chooses no value, dropping the field.</summary>
    /// <param name="fieldPath">Field path.</param>
    /// <returns>The decision.</returns>
    public static FieldChoice Empty(string fieldPath) => new(fieldPath, FieldChoiceKind.Empty);
}
