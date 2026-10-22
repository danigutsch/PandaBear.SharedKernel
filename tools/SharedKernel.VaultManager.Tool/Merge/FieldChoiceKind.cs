namespace PandaBear.VaultManager.Tool.Merge;

/// <summary>What a field decision selects.</summary>
internal enum FieldChoiceKind
{
    /// <summary>The value of the member named by <see cref="FieldChoice.OriginKey"/>.</summary>
    Member = 0,

    /// <summary>No value: every member's value for the field is dropped.</summary>
    Empty = 1,
}
