using PandaBear.VaultManager.Tool.Attachments;
using PandaBear.VaultManager.Tool.Model;

namespace PandaBear.VaultManager.Tool.Formats.Bitwarden;

/// <summary>
/// Reads Bitwarden zip exports: <c>data.json</c> plus <c>attachments/&lt;sanitized item name&gt;[_N]/&lt;file&gt;</c>.
/// The export names attachment folders after items, not item IDs, so a folder is attached only when exactly one item
/// can have produced it. The files of any other folder are returned as an <see cref="UnassignedAttachmentFolder"/>.
/// </summary>
internal static class BitwardenZipReader
{
    private const string DataEntry = "data.json";
    private const string AttachmentsPrefix = "attachments/";

    /// <summary>Reads a Bitwarden zip export.</summary>
    /// <param name="path">Zip file.</param>
    /// <returns>
    /// Items with attachment references, the attachment folders that match no item or several items with their files, and
    /// warnings that name those folders and other entries that could not be used.
    /// </returns>
    /// <exception cref="ArgumentException"><paramref name="path"/> is <see langword="null"/> or empty.</exception>
    /// <exception cref="InvalidDataException">The archive breaks a safety limit or holds no Bitwarden export.</exception>
    /// <exception cref="IOException">The file cannot be read.</exception>
    public static VaultReadResult Read(string path)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);

        string sourceFile = Path.GetFileName(path);
        using SafeArchive archive = SafeArchive.Open(path);
        ArchiveEntryInfo data = archive.Find(DataEntry)
            ?? throw new InvalidDataException("Bitwarden zip export has no data.json entry.");
        VaultExport export = BitwardenJsonReader.Read(archive.ReadUtf8(data), sourceFile);

        var warnings = new List<string>();
        var unassigned = new List<UnassignedAttachmentFolder>();
        var attachments = new Dictionary<VaultItem, List<AttachmentRef>>(ReferenceEqualityComparer.Instance);
        foreach (IGrouping<string, ArchiveEntryInfo> folder in GroupByFolder(archive.Entries, warnings))
        {
            AttachmentRef[] files = [.. folder.Select(entry => new AttachmentRef(entry.Path[(entry.Path.LastIndexOf('/') + 1)..], entry.Size, entry.Path))];
            VaultItem[] candidates = [.. export.Items.Where(item => Produces(item.Name, folder.Key))];
            if (candidates.Length != 1)
            {
                warnings.Add(candidates.Length == 0
                    ? $"Attachment folder \"{folder.Key}\" matches no item; its files were not attached."
                    : $"Attachment folder \"{folder.Key}\" matches {candidates.Length} items; its files were not attached.");
                unassigned.Add(new UnassignedAttachmentFolder(sourceFile, folder.Key, candidates.Length, files));
                continue;
            }

            if (!attachments.TryGetValue(candidates[0], out List<AttachmentRef>? list))
            {
                attachments.Add(candidates[0], list = []);
            }

            list.AddRange(files);
        }

        VaultItem[] items = [.. export.Items.Select(item => attachments.TryGetValue(item, out List<AttachmentRef>? assigned)
            ? item with { Attachments = [.. item.Attachments, .. assigned] }
            : item)];
        return new VaultReadResult(new VaultExport(items), warnings) { UnassignedAttachmentFolders = unassigned };
    }

    /// <summary>Folder name the Bitwarden export uses for an item: reserved characters become <c>_</c>, runs of <c>_</c> collapse.</summary>
    /// <param name="itemName">Item name.</param>
    /// <returns>Sanitized folder name without the duplicate suffix.</returns>
    public static string SanitizeFolderName(string itemName)
    {
        ArgumentNullException.ThrowIfNull(itemName);

        var builder = new System.Text.StringBuilder(itemName.Length);
        foreach (char character in itemName)
        {
            char mapped = character is '/' or '\\' or '>' or '<' or ':' or '"' or '|' or '?' or '*' ? '_' : character;
            if (mapped != '_' || builder.Length == 0 || builder[^1] != '_')
            {
                builder.Append(mapped);
            }
        }

        return builder.ToString();
    }

    private static IEnumerable<IGrouping<string, ArchiveEntryInfo>> GroupByFolder(IReadOnlyList<ArchiveEntryInfo> entries, List<string> warnings)
    {
        var files = new List<(string Folder, ArchiveEntryInfo Entry)>();
        foreach (ArchiveEntryInfo entry in entries)
        {
            if (entry.Path == DataEntry)
            {
                continue;
            }

            string[] parts = entry.Path.Split('/');
            if (parts.Length == 3 && entry.Path.StartsWith(AttachmentsPrefix, StringComparison.Ordinal) && parts[1].Length > 0 && parts[2].Length > 0)
            {
                files.Add((parts[1], entry));
            }
            else
            {
                warnings.Add($"Zip entry \"{entry.Path}\" is not a Bitwarden attachment and was ignored.");
            }
        }

        return files.GroupBy(file => file.Folder, file => file.Entry, StringComparer.Ordinal);
    }

    // A folder "X_N" comes from the (N+1)th item named X, or from an item whose sanitized name is literally "X_N".
    private static bool Produces(string itemName, string folder)
    {
        string sanitized = SanitizeFolderName(itemName);
        if (string.Equals(sanitized, folder, StringComparison.Ordinal))
        {
            return true;
        }

        int underscore = folder.LastIndexOf('_');
        return underscore > 0
            && underscore + 1 < folder.Length
            && folder[(underscore + 1)..].All(char.IsAsciiDigit)
            && string.Equals(sanitized, folder[..underscore], StringComparison.Ordinal);
    }
}
