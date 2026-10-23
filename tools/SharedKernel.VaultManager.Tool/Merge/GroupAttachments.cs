using PandaBear.VaultManager.Tool.Model;

namespace PandaBear.VaultManager.Tool.Merge;

/// <summary>
/// Chooses the files of a group of matched items. Each attachment of a member is a file of its own, even when two share a
/// name or a size. An attachment of another member is a copy of a chosen file only when both were read from their
/// archives and have the same name and <see cref="AttachmentRef.ContentHash"/>; the n-th such attachment of a member is a
/// copy of the n-th chosen file. A file whose content is unknown is kept for every member that has it.
/// </summary>
internal static class GroupAttachments
{
    /// <summary>
    /// Chooses the files of a group. Members are visited newest first, as the merge orders them, so a file found on several
    /// members keeps the newest member's copy.
    /// </summary>
    /// <param name="members">Group members, in any order.</param>
    /// <returns>Each chosen file with the member it was taken from, newest member first.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="members"/> is <see langword="null"/>.</exception>
    public static IReadOnlyList<(VaultItem Member, AttachmentRef Attachment)> Choose(IReadOnlyList<VaultItem> members)
    {
        ArgumentNullException.ThrowIfNull(members);

        var files = new List<(VaultItem Member, AttachmentRef Attachment)>();
        var chosen = new Dictionary<(string FileName, string ContentHash), int>();
        foreach (VaultItem member in members.OrderByDescending(member => member.LastModified.HasValue).ThenByDescending(member => member.LastModified))
        {
            var occurrences = new Dictionary<(string FileName, string ContentHash), int>();
            foreach (AttachmentRef attachment in member.Attachments)
            {
                if (attachment.ContentHash is not { } contentHash)
                {
                    files.Add((member, attachment));
                    continue;
                }

                var file = (attachment.FileName, contentHash);
                int occurrence = occurrences.GetValueOrDefault(file) + 1;
                occurrences[file] = occurrence;
                if (occurrence > chosen.GetValueOrDefault(file))
                {
                    chosen[file] = occurrence;
                    files.Add((member, attachment));
                }
            }
        }

        return files;
    }
}
