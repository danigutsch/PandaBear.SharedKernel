namespace PandaBear.VaultManager.Tool.Model;

/// <summary>
/// The Text custom field that lists, one per line, the other folders of an item. A Bitwarden individual vault holds one
/// folder per item, so the other folders of a Keeper record, the other collections of a Bitwarden item, and the folders
/// of merged items that lost are listed in it.
/// </summary>
internal static class AdditionalFolders
{
    /// <summary>Name of the custom field.</summary>
    public const string FieldName = "Additional folders";

    /// <summary>Folders listed by the item's additional folders fields, in order.</summary>
    /// <param name="fields">Custom fields of an item.</param>
    /// <returns>Trimmed, non-empty lines of every Text field named <see cref="FieldName"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="fields"/> is <see langword="null"/>.</exception>
    public static IEnumerable<string> Read(IEnumerable<CustomField> fields)
    {
        ArgumentNullException.ThrowIfNull(fields);

        return fields.Where(IsField)
            .SelectMany(field => (field.Value ?? "").Split('\n'))
            .Select(line => line.Trim())
            .Where(line => line.Length > 0);
    }

    /// <summary>
    /// Lists more folders in the item's additional folders field. The fields named <see cref="FieldName"/> become one
    /// field, after the other fields, that lists their folders and then <paramref name="folders"/>.
    /// </summary>
    /// <param name="fields">Custom fields of an item.</param>
    /// <param name="folders">Folders to add.</param>
    /// <param name="folder">The item's folder, which is not listed; <see langword="null"/> for none.</param>
    /// <returns>The fields, without an additional folders field when no folder remains.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="fields"/> or <paramref name="folders"/> is <see langword="null"/>.</exception>
    public static IReadOnlyList<CustomField> Add(IEnumerable<CustomField> fields, IEnumerable<string> folders, string? folder)
    {
        ArgumentNullException.ThrowIfNull(fields);
        ArgumentNullException.ThrowIfNull(folders);

        CustomField[] all = [.. fields];
        string[] listed = [.. Read(all).Concat(folders.Select(path => path.Trim()))
            .Where(path => path.Length > 0 && !string.Equals(path, folder, StringComparison.Ordinal))
            .Distinct(StringComparer.Ordinal)];
        if (listed.Length == 0 && !all.Any(IsField))
        {
            return all;
        }

        CustomField[] others = [.. all.Where(field => !IsField(field))];
        return listed.Length == 0 ? others : [.. others, new CustomField(FieldName, string.Join('\n', listed), CustomFieldType.Text)];
    }

    private static bool IsField(CustomField field) =>
        field.Type == CustomFieldType.Text && string.Equals(field.Name, FieldName, StringComparison.Ordinal);
}
