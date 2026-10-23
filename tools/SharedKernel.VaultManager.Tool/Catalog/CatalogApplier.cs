using System.Globalization;
using System.Text;
using PandaBear.VaultManager.Tool.Merge;
using PandaBear.VaultManager.Tool.Model;
using PandaBear.VaultManager.Tool.Pipeline;
using PandaBear.VaultManager.Tool.Secrets;

namespace PandaBear.VaultManager.Tool.Catalog;

/// <summary>
/// Applies an edited catalog to the vault it was made from. Only what the catalog shows changes: names, folders, additional
/// folders, favorites, shared notes, website addresses that are not masked, and shown text fields. Secrets, withheld
/// notes, protected fields, and masked addresses stay as they are. Items are never added or removed. Each change is
/// described by names, folders, and unmasked addresses only, and the removals among them are listed apart.
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
    /// The edited vault, the number of changed items, and a line for each change and removal, or the problems that keep
    /// the catalog from being applied. A line and a problem name the item by its key and name, never a value.
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
        var changes = new Changes();
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
            IReadOnlyList<SecretFinding> findings = SecretScanner.Default.Scan(source.Item);
            VaultItem edited = Edit(source, entry, findings, decisions, itemProblems);
            problems.AddRange(itemProblems.Select(problem => $"{source.OriginKey} (\"{source.Item.Name}\"): {problem}"));
            // An item counts as changed when a line describes a change of it.
            int lines = changes.Count;
            DescribeItem(changes.For($"{source.OriginKey} (\"{source.Item.Name}\"): "), source, entry, edited, findings, decisions);
            changed += changes.Count > lines ? 1 : 0;
            items.Add(edited);
        }

        if (problems.Count > 0)
        {
            return new CatalogApplyResult(null, 0, problems);
        }

        DescribeFolders(changes.For(""), vault, catalog, items);
        return new CatalogApplyResult(new VaultExport(items) { Folders = catalog.Folders }, changed, [])
        {
            Changes = changes.Lines,
            Removals = changes.RemovalLines,
        };
    }

    // Describes what the catalog changes in an item, by names, folders, and unmasked addresses, never by a value.
    private static void DescribeItem(Action<string, bool> describe, SourceItem source, CatalogItem entry, VaultItem edited, IReadOnlyList<SecretFinding> findings, MergeDecisions decisions)
    {
        VaultItem item = source.Item;
        if (!string.Equals(item.Name, edited.Name, StringComparison.Ordinal))
        {
            describe($"renamed to \"{edited.Name}\"", false);
        }

        if (!string.Equals(item.FolderPath, edited.FolderPath, StringComparison.Ordinal))
        {
            describe($"moved from folder {FolderName(item.FolderPath)} to {FolderName(edited.FolderPath)}", false);
        }

        string[] foldersBefore = [.. AdditionalFolders.Read(item.Fields).Distinct(StringComparer.Ordinal)];
        string[] foldersAfter = [.. AdditionalFolders.Read(edited.Fields).Distinct(StringComparer.Ordinal)];
        foreach (string folder in foldersAfter.Except(foldersBefore, StringComparer.Ordinal))
        {
            describe($"additional folder \"{folder}\" added", false);
        }

        foreach (string folder in foldersBefore.Except(foldersAfter, StringComparer.Ordinal))
        {
            describe($"additional folder \"{folder}\" removed", false);
        }

        if (!foldersBefore.Intersect(foldersAfter, StringComparer.Ordinal).SequenceEqual(foldersAfter.Intersect(foldersBefore, StringComparer.Ordinal), StringComparer.Ordinal))
        {
            describe("additional folders reordered", false);
        }

        if (item.Favorite != edited.Favorite)
        {
            describe(edited.Favorite ? "marked as a favorite" : "no longer a favorite", false);
        }

        DescribeUris(describe, item.Login?.Uris ?? [], entry.Uris);
        DescribeFields(describe, item.Fields, entry.Fields, findings);
        DescribeNotes(describe, source.OriginKey, item, entry.Notes, edited, decisions);
    }

    private static string FolderName(string? folder) => folder is null ? "no folder" : $"\"{folder}\"";

    // Unmasked addresses are compared by value as multisets, after the trimming Uris applies. A masked address is named by
    // its position only.
    private static void DescribeUris(Action<string, bool> describe, IReadOnlyList<LoginUri> current, IReadOnlyList<CatalogUri> entries)
    {
        List<LoginUri> before = [.. current
            .Where((uri, index) => CatalogBuilder.ToCatalog(uri, index).Masked is null)
            .Select(uri => uri with { Value = uri.Value.Trim() })];
        List<LoginUri> after = [.. entries
            .Where(entry => entry.Masked is null && entry.Index is null && entry.Value is not null)
            .Select(entry => new LoginUri(entry.Value!.Trim(), entry.Match))];
        // Each address after is paired with the first unpaired address before of the same value.
        bool[] paired = new bool[before.Count];
        var added = new List<LoginUri>();
        var kept = new List<(int Before, LoginUri After)>();
        foreach (LoginUri uri in after)
        {
            int index = Enumerable.Range(0, before.Count)
                .FirstOrDefault(candidate => !paired[candidate] && string.Equals(before[candidate].Value, uri.Value, StringComparison.Ordinal), -1);
            if (index < 0)
            {
                added.Add(uri);
                continue;
            }

            paired[index] = true;
            kept.Add((index, uri));
        }

        for (int index = 0; index < before.Count; index++)
        {
            if (!paired[index])
            {
                describe($"website address \"{Visible(before[index].Value)}\" removed", true);
            }
        }

        foreach (LoginUri uri in added)
        {
            describe($"website address \"{Visible(uri.Value)}\" added{Wider(uri.Match)}", false);
        }

        foreach ((_, LoginUri now) in kept.Where(pair => before[pair.Before].Match != pair.After.Match))
        {
            describe($"website address \"{Visible(now.Value)}\" now matches by {now.Match?.ToString() ?? "the default"}{Wider(now.Match)}", false);
        }

        if (!kept.Select(pair => pair.Before).SequenceEqual(kept.Select(pair => pair.Before).Order()))
        {
            describe("website addresses reordered", false);
        }

        for (int position = 0; position < entries.Count; position++)
        {
            if (entries[position].Index is { } index && index != position)
            {
                describe(string.Create(CultureInfo.InvariantCulture, $"masked website address {index} moved"), false);
            }
        }
    }

    // Letters of other scripts can look like Latin ones, so an address shows every character outside printable ASCII as
    // \uXXXX: a look-alike host cannot pass for the one it replaces.
    private static string Visible(string address)
    {
        var visible = new StringBuilder(address.Length);
        foreach (char character in address)
        {
            if (character is >= ' ' and <= '~')
            {
                visible.Append(character);
            }
            else
            {
                visible.Append(CultureInfo.InvariantCulture, $"\\u{(int)character:X4}");
            }
        }

        return visible.ToString();
    }

    private static string Wider(UriMatch? match) =>
        match is UriMatch.RegularExpression or UriMatch.StartsWith ? " (offers the login on more sites)" : "";

    // Exact (name, value) matches pair first; the rest pair by name in order. Only names are described.
    private static void DescribeFields(Action<string, bool> describe, IReadOnlyList<CustomField> current, IReadOnlyList<CatalogField> shown, IReadOnlyList<SecretFinding> findings)
    {
        List<(string Name, string Value)> before = [.. current
            .Select((field, index) => (Field: field, Index: index))
            .Where(pair => !ShownValues.IsAdditionalFolders(pair.Field) && ShownValues.IsShown(pair.Field, pair.Index, findings))
            .Select(pair => (pair.Field.Name ?? "", pair.Field.Value ?? ""))];
        List<(string Name, string Value)> after = [.. shown.Select(field => (field.Name ?? "", field.Value ?? ""))];
        var remainingBefore = new List<(string Name, string Value)>();
        var kept = new List<(string Name, string Value)>();
        foreach ((string Name, string Value) field in before)
        {
            if (after.Remove(field))
            {
                kept.Add(field);
            }
            else
            {
                remainingBefore.Add(field);
            }
        }

        // The fields kept as they are, in the order the catalog lists them.
        var keptAfter = new List<(string Name, string Value)>(kept);
        List<(string Name, string Value)> listed = [.. shown.Select(field => (field.Name ?? "", field.Value ?? ""))];
        if (!listed.Where(field => keptAfter.Remove(field)).SequenceEqual(kept))
        {
            describe("text fields reordered", false);
        }

        foreach ((string name, _) in remainingBefore)
        {
            int index = after.FindIndex(field => string.Equals(field.Name, name, StringComparison.Ordinal));
            if (index < 0)
            {
                describe($"text field \"{name}\" removed", true);
                continue;
            }

            after.RemoveAt(index);
            describe($"text field \"{name}\" changed", true);
        }

        foreach ((string name, _) in after)
        {
            describe($"text field \"{name}\" added", false);
        }
    }

    private static void DescribeNotes(Action<string, bool> describe, string originKey, VaultItem item, CatalogNotes? entry, VaultItem edited, MergeDecisions decisions)
    {
        if (string.IsNullOrEmpty(item.Notes))
        {
            if (edited.Notes is not null)
            {
                describe("notes added", false);
            }
        }
        else if (entry?.Decision == NotesDecision.Share && decisions.SharesNotes(originKey, item.Notes))
        {
            if (edited.Notes is null)
            {
                describe("notes emptied", true);
            }
            else if (!string.Equals(item.Notes, edited.Notes, StringComparison.Ordinal))
            {
                describe("notes edited", false);
            }
        }
        else if (edited.Notes is null)
        {
            describe($"notes moved into a hidden field named \"{HiddenNotesField}\"", false);
        }
    }

    private static void DescribeFolders(Action<string, bool> describe, LoadedVault vault, CatalogDocument catalog, IReadOnlyList<VaultItem> edited)
    {
        IReadOnlyList<string> before = FolderTree.WithParents(vault.Folders.Concat(vault.Items.Select(source => source.Item.FolderPath)));
        IReadOnlyList<string> after = FolderTree.WithParents(catalog.Folders.Concat(edited.Select(item => item.FolderPath)));
        foreach (string folder in after.Except(before, StringComparer.Ordinal))
        {
            describe($"folder \"{folder}\" added", false);
        }

        foreach (string folder in vault.Folders.Where(folder => !string.IsNullOrWhiteSpace(folder)).Distinct(StringComparer.Ordinal).Except(after, StringComparer.Ordinal))
        {
            describe($"folder \"{folder}\" removed", true);
        }
    }

    private static VaultItem Edit(SourceItem source, CatalogItem entry, IReadOnlyList<SecretFinding> findings, MergeDecisions decisions, List<string> problems)
    {
        VaultItem item = source.Item;
        if (string.IsNullOrWhiteSpace(entry.Name))
        {
            problems.Add("the name is empty.");
        }

        string? folder = string.IsNullOrWhiteSpace(entry.Folder) ? null : entry.Folder.Trim();
        (string? notes, CustomField? hiddenNotes) = Notes(source.OriginKey, item, entry.Notes, decisions, problems);
        LoginData? login = item.Login is { } current ? current with { Uris = Uris(current.Uris, entry.Uris, problems) } : null;
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
            if (ShownValues.IsAdditionalFolders(field))
            {
                continue;
            }

            if (!ShownValues.IsShown(field, index, findings))
            {
                fields.Add(field);
            }
            else if (next < shown.Count)
            {
                // A field the catalog keeps as shown stays as read, so a missing name or value is not written as empty.
                CatalogField placed = shown[next++];
                bool unchanged = string.Equals(field.Name ?? string.Empty, placed.Name, StringComparison.Ordinal)
                    && string.Equals(field.Value ?? string.Empty, placed.Value, StringComparison.Ordinal);
                fields.Add(unchanged ? field : ToField(placed));
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

            // Notes the catalog keeps as shown stay as read, also when they hold only white space.
            return (string.Equals(entry.Text, item.Notes, StringComparison.Ordinal) ? item.Notes : NonBlank(entry.Text), null);
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
                    // An address the catalog keeps as shown stays as read, white space included.
                    result.Add(current.FirstOrDefault(uri => string.Equals(uri.Value, entry.Value, StringComparison.Ordinal) && uri.Match == entry.Match)
                        ?? new LoginUri(entry.Value.Trim(), entry.Match));
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

    // Collects change lines and the removals among them.
    private sealed class Changes
    {
        private readonly List<string> lines = [];
        private readonly List<string> removals = [];

        public IReadOnlyList<string> Lines => lines;

        public int Count => lines.Count;

        public IReadOnlyList<string> RemovalLines => removals;

        // Returns what adds a line with the prefix, and marks it a removal when asked.
        public Action<string, bool> For(string prefix) => (text, isRemoval) =>
        {
            string line = prefix + text;
            lines.Add(line);
            if (isRemoval)
            {
                removals.Add(line);
            }
        };
    }
}
