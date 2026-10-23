using System.Globalization;
using PandaBear.VaultManager.Tool.Merge;
using PandaBear.VaultManager.Tool.Model;
using PandaBear.VaultManager.Tool.Pipeline;
using PandaBear.VaultManager.Tool.Secrets;

namespace PandaBear.VaultManager.Tool.Catalog;

/// <summary>
/// Applies an edited catalog to the vault it was made from. Only what the catalog shows changes: names, folders, additional
/// folders, favorites, shared notes, website addresses that are not masked, and shown text fields. Secrets, withheld
/// notes, protected fields, and masked addresses stay as they are. Items are never added or removed.
/// </summary>
internal static class CatalogApplier
{
    /// <summary>Name of the hidden field that receives notes the user decided to hide.</summary>
    public const string HiddenNotesField = "Notes";

    /// <summary>Applies a catalog.</summary>
    /// <param name="vault">Items read from the file the catalog was made from.</param>
    /// <param name="catalog">Edited catalog.</param>
    /// <param name="decisions">Decisions that hold the notes decisions.</param>
    /// <returns>
    /// The edited vault and the number of changed items, or the problems that keep the catalog from being applied. A
    /// problem names the item by its key and name, never a value.
    /// </returns>
    public static CatalogApplyResult Apply(LoadedVault vault, CatalogDocument catalog, MergeDecisions decisions)
    {
        ArgumentNullException.ThrowIfNull(vault);
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(decisions);

        var problems = new List<string>();
        Dictionary<string, CatalogItem> entries = catalog.Items.ToDictionary(item => item.Key, StringComparer.Ordinal);
        var known = new HashSet<string>(vault.Items.Select(source => source.OriginKey), StringComparer.Ordinal);
        problems.AddRange(catalog.Items.Where(item => !known.Contains(item.Key)).Select(item => $"{item.Key}: the vault has no item with this key; items cannot be added."));

        var items = new List<VaultItem>(vault.Items.Count);
        int changed = 0;
        foreach (SourceItem source in vault.Items)
        {
            if (!entries.TryGetValue(source.OriginKey, out CatalogItem? entry))
            {
                problems.Add($"{source.OriginKey} (\"{source.Item.Name}\"): the catalog has no entry for this item; items cannot be removed.");
                items.Add(source.Item);
                continue;
            }

            var itemProblems = new List<string>();
            VaultItem edited = Edit(source, entry, decisions, itemProblems);
            problems.AddRange(itemProblems.Select(problem => $"{source.OriginKey} (\"{source.Item.Name}\"): {problem}"));
            changed += Changed(source.Item, edited) ? 1 : 0;
            items.Add(edited);
        }

        return problems.Count > 0
            ? new CatalogApplyResult(null, 0, problems)
            : new CatalogApplyResult(new VaultExport(items) { Folders = catalog.Folders }, changed, []);
    }

    private static VaultItem Edit(SourceItem source, CatalogItem entry, MergeDecisions decisions, List<string> problems)
    {
        VaultItem item = source.Item;
        IReadOnlyList<SecretFinding> findings = SecretScanner.Default.Scan(item);
        if (string.IsNullOrWhiteSpace(entry.Name))
        {
            problems.Add("the name is empty.");
        }

        string? folder = string.IsNullOrWhiteSpace(entry.Folder) ? null : entry.Folder.Trim();
        (string? notes, CustomField? hiddenNotes) = Notes(source.OriginKey, item, entry.Notes, decisions, problems);
        LoginData? login = item.Login is { } current ? current with
        {
            Uris = Uris(current.Uris, entry.Uris, problems)
        } : null;
        if (item.Login is null && entry.Uris.Count > 0)
        {
            problems.Add("only a login has website addresses.");
        }

        List<CustomField> fields = Fields(item.Fields, entry.Fields, findings);
        if (hiddenNotes is not null)
        {
            fields.Add(hiddenNotes);
        }

        return item with
        {
            Name = string.IsNullOrWhiteSpace(entry.Name) ? item.Name : entry.Name,
            FolderPath = folder,
            Favorite = entry.Favorite,
            Notes = notes,
            Login = login,
            Fields = AdditionalFolders.Add(fields, entry.AdditionalFolders, folder),
        };
    }

    // Protected fields keep their places. The shown fields of the catalog fill the places of the shown fields as read, in
    // order, and the rest follow, so an unedited catalog keeps the field order.
    private static List<CustomField> Fields(IReadOnlyList<CustomField> current, IReadOnlyList<CatalogField> shown, IReadOnlyList<SecretFinding> findings)
    {
        var fields = new List<CustomField>(current.Count + shown.Count);
        int next = 0;
        for (int index = 0; index < current.Count; index++)
        {
            CustomField field = current[index];
            if (CatalogBuilder.IsAdditionalFolders(field))
            {
                continue;
            }

            if (!CatalogBuilder.IsShown(field, index, findings))
            {
                fields.Add(field);
            }
            else if (next < shown.Count)
            {
                fields.Add(ToField(shown[next++]));
            }
        }

        fields.AddRange(shown.Skip(next).Select(ToField));
        return fields;
    }

    private static CustomField ToField(CatalogField field) => new(field.Name, field.Value, CustomFieldType.Text);

    // Shared notes take the catalog's text; other notes stay as read, and hidden notes move into a hidden field. The
    // decisions file, not the catalog, says which notes are shared, and only for the notes it was made for, so editing the
    // catalog cannot reveal or replace them.
    private static (string? Notes, CustomField? Hidden) Notes(string originKey, VaultItem item, CatalogNotes? entry, MergeDecisions decisions, List<string> problems)
    {
        if (string.IsNullOrEmpty(item.Notes))
        {
            return (NonBlank(entry?.Text), null);
        }

        if (entry is null)
        {
            problems.Add("the notes entry was removed; notes are removed by sharing them and emptying their text.");
            return (item.Notes, null);
        }

        if (entry.Decision == NotesDecision.Share)
        {
            if (!decisions.SharesNotes(originKey, item.Notes))
            {
                problems.Add("the catalog shares the notes, but the decisions file does not. Share them in the decisions file and run catalog again.");
                return (item.Notes, null);
            }

            return (NonBlank(entry.Text), null);
        }

        if (entry.Text is not null)
        {
            problems.Add("the notes are withheld, so their text cannot be set. Share them in the decisions file and run catalog again.");
        }

        return decisions.FindNotes(originKey) == NotesDecision.Hidden
            ? (null, new CustomField(HiddenNotesField, item.Notes, CustomFieldType.Hidden))
            : (item.Notes, null);
    }

    // A masked address comes back from its position in the item as read, so its credential parts are never retyped. Each
    // one must appear exactly once with the mask the catalog showed.
    private static List<LoginUri> Uris(IReadOnlyList<LoginUri> current, IReadOnlyList<CatalogUri> entries, List<string> problems)
    {
        var result = new List<LoginUri>(entries.Count);
        var maskedSeen = new HashSet<int>();
        foreach (CatalogUri entry in entries)
        {
            if (entry.Masked is null && entry.Index is null)
            {
                if (string.IsNullOrWhiteSpace(entry.Value))
                {
                    problems.Add("a website address is empty.");
                }
                else
                {
                    result.Add(new LoginUri(entry.Value.Trim(), entry.Match));
                }

                continue;
            }

            if (entry.Index is not { } index || index < 0 || index >= current.Count || entry.Value is not null
                || CatalogBuilder.ToCatalog(current[index], index) is not { Masked: { } masked }
                || !string.Equals(masked, entry.Masked, StringComparison.Ordinal)
                || !maskedSeen.Add(index))
            {
                problems.Add(string.Create(CultureInfo.InvariantCulture, $"masked website address {entry.Index?.ToString(CultureInfo.InvariantCulture) ?? "?"} was changed; a masked address can only be moved."));
                continue;
            }

            result.Add(current[index]);
        }

        for (int index = 0; index < current.Count; index++)
        {
            if (CatalogBuilder.ToCatalog(current[index], index).Masked is not null && !maskedSeen.Contains(index))
            {
                problems.Add(string.Create(CultureInfo.InvariantCulture, $"masked website address {index} was removed; a masked address can only be moved."));
            }
        }

        return result;
    }

    private static string? NonBlank(string? text) => string.IsNullOrWhiteSpace(text) ? null : text;

    private static bool Changed(VaultItem before, VaultItem after) =>
        !string.Equals(before.Name, after.Name, StringComparison.Ordinal)
        || !string.Equals(before.FolderPath, after.FolderPath, StringComparison.Ordinal)
        || before.Favorite != after.Favorite
        || !string.Equals(before.Notes, after.Notes, StringComparison.Ordinal)
        || !before.Fields.SequenceEqual(after.Fields)
        || !(before.Login?.Uris ?? []).SequenceEqual(after.Login?.Uris ?? []);
}
