using System.Globalization;
using PandaBear.VaultManager.Tool.Matching;
using PandaBear.VaultManager.Tool.Model;

namespace PandaBear.VaultManager.Tool.Merge;

/// <summary>
/// Semantic comparison of items, field by field. Comparison keys stay in memory and are never reported.
/// </summary>
/// <remarks>
/// Single values compare by <see cref="ScalarField.Key"/>, with absent values (null, empty, or whitespace) equal to each
/// other. Addresses compare as a set of <see cref="ItemNormalizer.Uri"/> keys, custom fields as a set of name, value
/// with normalized line endings, type, and link, passkeys by credential ID, and password history by password.
/// </remarks>
internal static class FieldDiff
{
    private const char Separator = '\u001f';

    private static readonly string[] Order =
    [
        FieldPaths.Type,
        FieldPaths.Name,
        FieldPaths.Folder,
        FieldPaths.Notes,
        FieldPaths.Favorite,
        FieldPaths.Reprompt,
        FieldPaths.LoginUsername,
        FieldPaths.LoginPassword,
        FieldPaths.LoginTotp,
        FieldPaths.LoginUris,
        FieldPaths.LoginFido2Credentials,
        FieldPaths.Fields,
        FieldPaths.PasswordHistory,
    ];

    private static readonly (string Path, Func<VaultItem, string> Key)[] Comparers =
    [
        (FieldPaths.Type, item => ((int)item.Type).ToString(CultureInfo.InvariantCulture)),
        (FieldPaths.Name, item => ItemNormalizer.Title(item.Name)),
        (FieldPaths.Folder, item => item.FolderPath ?? string.Empty),
        (FieldPaths.Favorite, item => item.Favorite ? "1" : "0"),
        (FieldPaths.Reprompt, item => item.Reprompt ? "1" : "0"),
        (FieldPaths.LoginUris, item => SetKey(item.Login?.Uris ?? [], uri => UriKey(uri))),
        (FieldPaths.LoginFido2Credentials, item => SetKey(item.Login?.Fido2Credentials ?? [], PasskeyKey)),
        (FieldPaths.Fields, item => SetKey(item.Fields, FieldKey)),
        (FieldPaths.PasswordHistory, item => SetKey(item.PasswordHistory, entry => entry.Password)),
    ];

    /// <summary>Field paths on which the members differ, in report order.</summary>
    /// <param name="members">Group members.</param>
    /// <returns>Changed paths; empty when every member is semantically equal.</returns>
    public static IReadOnlyList<string> ChangedPaths(IReadOnlyList<VaultItem> members)
    {
        ArgumentNullException.ThrowIfNull(members);

        var changed = new List<string>();
        foreach (var (path, key) in Comparers)
        {
            if (Differs(members, key))
            {
                changed.Add(path);
            }
        }

        foreach (var field in ScalarFields.All)
        {
            if (Differs(members, item => ScalarKey(field, item)))
            {
                changed.Add(field.Path);
            }
        }

        return [.. changed.OrderBy(ReportOrder)];
    }

    /// <summary>Position of a field path in report order.</summary>
    /// <param name="path">Field path.</param>
    /// <returns>Sort position.</returns>
    public static int ReportOrder(string path)
    {
        int index = Array.IndexOf(Order, path);
        if (index >= 0)
        {
            return index;
        }

        for (int scalar = 0; scalar < ScalarFields.All.Count; scalar++)
        {
            if (string.Equals(ScalarFields.All[scalar].Path, path, StringComparison.Ordinal))
            {
                return Order.Length + scalar;
            }
        }

        return int.MaxValue;
    }

    /// <summary>Comparison key of a present single value, or empty when the value is absent.</summary>
    /// <param name="field">Field.</param>
    /// <param name="item">Item.</param>
    /// <returns>The key.</returns>
    public static string ScalarKey(ScalarField field, VaultItem item)
    {
        string? value = field.Get(item);
        return ItemNormalizer.IsAbsent(value) ? string.Empty : "=" + field.Key(value!);
    }

    /// <summary>Comparison key of a stored address.</summary>
    /// <param name="uri">Address.</param>
    /// <returns>The key.</returns>
    public static string UriKey(LoginUri uri) => ItemNormalizer.Uri(uri.Value);

    /// <summary>Comparison key of a passkey: its credential ID.</summary>
    /// <param name="passkey">Passkey.</param>
    /// <returns>The key.</returns>
    public static string PasskeyKey(Fido2Credential passkey) => passkey.CredentialId;

    /// <summary>Comparison key of a custom field: name, value with normalized line endings, type, and link.</summary>
    /// <param name="field">Custom field.</param>
    /// <returns>The key.</returns>
    public static string FieldKey(CustomField field) => string.Join(
        Separator,
        field.Name ?? string.Empty,
        ItemNormalizer.Notes(field.Value),
        ((int)field.Type).ToString(CultureInfo.InvariantCulture),
        field.LinkedId?.ToString(CultureInfo.InvariantCulture) ?? string.Empty);

    private static bool Differs(IReadOnlyList<VaultItem> members, Func<VaultItem, string> key)
    {
        string first = key(members[0]);
        for (int index = 1; index < members.Count; index++)
        {
            if (!string.Equals(first, key(members[index]), StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static string SetKey<T>(IEnumerable<T> values, Func<T, string> key) =>
        string.Join('\u001e', values.Select(key).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal));
}
