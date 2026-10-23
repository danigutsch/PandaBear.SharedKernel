namespace PandaBear.VaultManager.Tool.Merge;

/// <summary>User decision for one field of one group. It names a member, never a value.</summary>
/// <param name="FieldPath">Field path, one of <see cref="FieldPaths"/>.</param>
/// <param name="Kind">What the decision selects.</param>
/// <param name="OriginKey">Origin key of the chosen member for <see cref="FieldChoiceKind.Member"/>; otherwise <see langword="null"/>.</param>
/// <param name="Options">
/// Members the field can take its value from, listed in a <see cref="FieldChoiceKind.Pending"/> entry so the user can
/// pick one. Informational: reading decisions ignores it.
/// </param>
internal sealed record FieldChoice(string FieldPath, FieldChoiceKind Kind, string? OriginKey = null, IReadOnlyList<FieldChoiceOption>? Options = null)
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

    /// <summary>Marks a field that needs a decision. It counts as no decision.</summary>
    /// <param name="fieldPath">Field path.</param>
    /// <param name="options">Members the field can take its value from.</param>
    /// <returns>The pending entry.</returns>
    public static FieldChoice Pending(string fieldPath, IReadOnlyList<FieldChoiceOption> options) => new(fieldPath, FieldChoiceKind.Pending, Options: options);
}
