namespace PandaBear.VaultManager.Tool.Merge;

/// <summary>
/// How the notes of an item are handled in a catalog. The zero value is <see cref="Pending"/>, so notes are withheld until
/// the user decides.
/// </summary>
internal enum NotesDecision
{
    /// <summary>No decision yet; the catalog withholds the notes, and <c>apply</c> keeps them as they are.</summary>
    Pending = 0,

    /// <summary>The catalog shows the notes, and <c>apply</c> takes the notes text the catalog holds.</summary>
    Share = 1,

    /// <summary>The catalog withholds the notes, and <c>apply</c> keeps them as they are.</summary>
    Private = 2,

    /// <summary>The catalog withholds the notes, and <c>apply</c> moves them into a hidden custom field named <c>Notes</c>.</summary>
    Hidden = 3,
}
