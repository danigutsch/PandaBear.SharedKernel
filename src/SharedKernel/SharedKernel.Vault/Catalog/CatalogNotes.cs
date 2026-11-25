using System.Text;
using SharedKernel.Vault.Merge;

namespace SharedKernel.Vault.Catalog;

/// <summary>Notes of a catalog item. The text is shown only after the user shares it in the decisions file.</summary>
/// <param name="Decision">The user's decision on these notes when the catalog was made.</param>
/// <param name="Text">
/// The notes when <paramref name="Decision"/> is <see cref="NotesDecision.Share"/>; otherwise <see langword="null"/>,
/// and it must stay so.
/// </param>
public sealed record CatalogNotes(NotesDecision Decision, string? Text = null)
{
    /// <summary>Prints only non-secret members, so values never reach logs or messages through <c>ToString</c>.</summary>
    private bool PrintMembers(StringBuilder builder)
    {
        builder.Append("Decision = ").Append(Decision).Append(", HasText = ").Append(Text is not null);
        return true;
    }
}
