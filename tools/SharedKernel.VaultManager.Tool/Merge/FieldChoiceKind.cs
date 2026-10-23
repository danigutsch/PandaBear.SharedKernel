namespace PandaBear.VaultManager.Tool.Merge;

/// <summary>What a field decision selects. The zero value is <see cref="Pending"/>, which selects nothing.</summary>
internal enum FieldChoiceKind
{
    /// <summary>
    /// No decision yet; it counts as no decision. The field needs <see cref="Member"/> with one of
    /// <see cref="FieldChoice.Options"/>, or <see cref="Empty"/>.
    /// </summary>
    Pending = 0,

    /// <summary>The value of the member named by <see cref="FieldChoice.OriginKey"/>.</summary>
    Member = 1,

    /// <summary>No value: every member's value for the field is dropped.</summary>
    Empty = 2,
}
