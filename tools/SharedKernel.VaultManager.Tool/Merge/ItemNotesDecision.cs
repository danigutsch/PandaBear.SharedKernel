using System.Security.Cryptography;
using System.Text;

namespace PandaBear.VaultManager.Tool.Merge;

/// <summary>User decision on the notes of one input item. It names the item, never its notes.</summary>
/// <param name="OriginKey">Origin key of the item as read, from <see cref="Matching.OriginKeys"/>.</param>
/// <param name="Decision">How the notes are handled; <see cref="NotesDecision.Pending"/> counts as no decision.</param>
/// <param name="ItemName">Name of the item, so the user can find it. Informational: reading decisions ignores it.</param>
/// <param name="Findings">
/// Number of secret findings in the notes when the entry was written, so the user knows which notes to check first.
/// Informational: reading decisions ignores it.
/// </param>
/// <param name="NotesSha256">
/// For <see cref="NotesDecision.Share"/>, the <see cref="HashOf"/> of the notes that were shared. Notes that changed
/// since then are not shared, so a decision never shows text the user did not confirm. It is the hash of notes the
/// catalog already shows, so it reveals nothing the catalog does not.
/// </param>
internal sealed record ItemNotesDecision(string OriginKey, NotesDecision Decision, string? ItemName = null, int? Findings = null, string? NotesSha256 = null)
{
    /// <summary>Hashes notes for <see cref="NotesSha256"/>.</summary>
    /// <param name="notes">Notes text.</param>
    /// <returns>SHA-256 of the UTF-8 text, in lowercase hexadecimal.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="notes"/> is <see langword="null"/>.</exception>
    public static string HashOf(string notes)
    {
        ArgumentNullException.ThrowIfNull(notes);

        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(notes)));
    }
}
