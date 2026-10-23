using PandaBear.VaultManager.Tool.Merge;

namespace PandaBear.VaultManager.Tool.Catalog;

/// <summary>Notes of a catalog item. The text is shown only after the user shares it in the decisions file.</summary>
/// <param name="Decision">The user's decision on these notes when the catalog was made.</param>
/// <param name="Text">
/// The notes when <paramref name="Decision"/> is <see cref="NotesDecision.Share"/>; otherwise <see langword="null"/>,
/// and it must stay so.
/// </param>
internal sealed record CatalogNotes(NotesDecision Decision, string? Text = null);
