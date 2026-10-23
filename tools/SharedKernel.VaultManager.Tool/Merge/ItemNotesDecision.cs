namespace PandaBear.VaultManager.Tool.Merge;

/// <summary>User decision on the notes of one input item. It names the item, never its notes.</summary>
/// <param name="OriginKey">Origin key of the item as read, from <see cref="Matching.OriginKeys"/>.</param>
/// <param name="Decision">How the notes are handled; <see cref="NotesDecision.Pending"/> counts as no decision.</param>
/// <param name="ItemName">Name of the item, so the user can find it. Informational: reading decisions ignores it.</param>
/// <param name="Findings">
/// Number of secret findings in the notes when the entry was written, so the user knows which notes to check first.
/// Informational: reading decisions ignores it.
/// </param>
internal sealed record ItemNotesDecision(string OriginKey, NotesDecision Decision, string? ItemName = null, int? Findings = null);
