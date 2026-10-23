using PandaBear.VaultManager.Tool.Merge;
using PandaBear.VaultManager.Tool.Model;
using PandaBear.VaultManager.Tool.Pipeline;
using PandaBear.VaultManager.Tool.Secrets;

namespace PandaBear.VaultManager.Tool.Catalog;

/// <summary>
/// Makes the catalog of a vault. A catalog holds names, folders, favorites, website addresses with their credential
/// parts masked, the notes the user shared as they are, text custom fields without secret findings, user names without
/// secret findings, and attachment names. It never holds passwords, TOTPs, passkeys, password history, hidden fields, card,
/// identity, or SSH key values, withheld notes, or user names and custom fields that hold a secret finding.
/// </summary>
internal static class CatalogBuilder
{
    /// <summary>Makes the catalog of a vault read from one file.</summary>
    /// <param name="vault">Items read from the file.</param>
    /// <param name="sha256">SHA-256 of the file, from <see cref="CatalogFile.Hash"/>.</param>
    /// <param name="decisions">Decisions that hold the notes decisions.</param>
    /// <returns>The catalog, the notes entries to write to the decisions file, and counts for the summary.</returns>
    /// <exception cref="ArgumentException">The vault was not read from exactly one file.</exception>
    public static CatalogBuild Build(LoadedVault vault, string sha256, MergeDecisions decisions)
    {
        ArgumentNullException.ThrowIfNull(vault);
        ArgumentException.ThrowIfNullOrEmpty(sha256);
        ArgumentNullException.ThrowIfNull(decisions);
        if (vault.InputPaths.Count != 1)
        {
            throw new ArgumentException("A catalog is made from one vault file.", nameof(vault));
        }

        var items = new List<CatalogItem>(vault.Items.Count);
        var updates = new List<ItemNotesDecision>();
        var counts = new CatalogCounts();
        foreach (SourceItem source in vault.Items)
        {
            VaultItem item = source.Item;
            IReadOnlyList<SecretFinding> findings = SecretScanner.Default.Scan(item);
            CatalogNotes? notes = null;
            if (!string.IsNullOrEmpty(item.Notes))
            {
                (NotesDecision decision, ItemNotesDecision? update) = NotesDecisionOf(source.OriginKey, item, findings, decisions);
                notes = new CatalogNotes(decision, decision == NotesDecision.Share ? item.Notes : null);
                counts.Count(decision);
                if (update is not null)
                {
                    updates.Add(update);
                }
            }

            var fields = new List<CatalogField>();
            var protectedFields = new List<string>();
            for (int index = 0; index < item.Fields.Count; index++)
            {
                CustomField field = item.Fields[index];
                if (ShownValues.IsAdditionalFolders(field))
                {
                    continue;
                }

                if (ShownValues.IsShown(field, index, findings))
                {
                    fields.Add(new CatalogField(field.Name ?? string.Empty, field.Value ?? string.Empty));
                }
                else
                {
                    protectedFields.Add(field.Name ?? string.Empty);
                }
            }

            counts.ProtectedFields += protectedFields.Count;
            CatalogUri[] uris = [.. (item.Login?.Uris ?? []).Select((uri, index) => ToCatalog(uri, index))];
            counts.MaskedUris += uris.Count(uri => uri.Masked is not null);
            items.Add(new CatalogItem(
                source.OriginKey,
                item.Name,
                item.FolderPath,
                [.. AdditionalFolders.Read(item.Fields)],
                item.Favorite,
                notes,
                uris,
                fields,
                Info(item, findings, protectedFields)));
        }

        var catalog = new CatalogDocument(
            CatalogFile.CurrentVersion,
            new CatalogSource(Path.GetFileName(vault.InputPaths[0]), sha256),
            vault.Folders,
            items);
        return new CatalogBuild(catalog, updates, counts);
    }

    // A share decision shows the notes it was made for. One that names no notes, such as one typed into the decisions file,
    // is bound to the current notes; one made for other notes falls back to pending, so changed notes are never shown
    // without a new decision.
    private static (NotesDecision Decision, ItemNotesDecision? Update) NotesDecisionOf(
        string originKey, VaultItem item, IReadOnlyList<SecretFinding> findings, MergeDecisions decisions)
    {
        string notes = item.Notes!;
        int inNotes = findings.Count(finding => string.Equals(finding.Location.FieldPath, SecretLocation.Notes, StringComparison.Ordinal));
        ItemNotesDecision? entry = decisions.FindNotesEntry(originKey);
        NotesDecision decision = entry?.Decision ?? NotesDecision.Pending;
        if (decision == NotesDecision.Share && entry!.NotesSha256 is null)
        {
            return (decision, entry with { ItemName = item.Name, Findings = inNotes, NotesSha256 = ItemNotesDecision.HashOf(notes) });
        }

        if (decision == NotesDecision.Share && !decisions.SharesNotes(originKey, notes))
        {
            decision = NotesDecision.Pending;
        }

        return decision == NotesDecision.Pending
            ? (decision, new ItemNotesDecision(originKey, NotesDecision.Pending, item.Name, inNotes))
            : (decision, null);
    }

    /// <summary>Converts a website address, masking it when it holds user info, a query, or a fragment.</summary>
    /// <param name="uri">Address.</param>
    /// <param name="index">Position of the address in the item.</param>
    /// <returns>The catalog entry.</returns>
    public static CatalogUri ToCatalog(LoginUri uri, int index)
    {
        ArgumentNullException.ThrowIfNull(uri);

        string masked = ShownValues.MaskUri(uri.Value);
        return string.Equals(masked, uri.Value, StringComparison.Ordinal)
            ? new CatalogUri(uri.Value, uri.Match)
            : new CatalogUri(Masked: masked, Index: index);
    }

    private static CatalogInfo Info(VaultItem item, IReadOnlyList<SecretFinding> findings, IReadOnlyList<string> protectedFields)
    {
        LoginData? login = item.Login;
        return new CatalogInfo(
            item.Type,
            ShownValues.Username(item, findings),
            !string.IsNullOrEmpty(login?.Password),
            !string.IsNullOrEmpty(login?.Totp),
            login?.Fido2Credentials.Count ?? 0,
            item.PasswordHistory.Count,
            protectedFields,
            [.. item.Attachments.Select(attachment => new CatalogAttachment(attachment.FileName, attachment.Size))]);
    }
}
