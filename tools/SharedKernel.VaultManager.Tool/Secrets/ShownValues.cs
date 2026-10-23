using PandaBear.VaultManager.Tool.Model;

namespace PandaBear.VaultManager.Tool.Secrets;

/// <summary>
/// Which values of an item may be shown to a person or an agent who must not see its secrets, as the catalog and the
/// merge brief show them: text custom fields without a secret finding, website addresses with their user info, query, and
/// fragment masked, and user names without a secret finding.
/// </summary>
internal static class ShownValues
{
    /// <summary>Text that replaces the masked parts of a website address.</summary>
    public const string Mask = "***";

    /// <summary>
    /// Whether a custom field may be shown: a text field that is not the additional folders field and in whose name and
    /// value no secret finding lies.
    /// </summary>
    /// <param name="field">Custom field.</param>
    /// <param name="index">Position of the field in the item.</param>
    /// <param name="findings">Secret findings of the item.</param>
    /// <returns><see langword="true"/> when the field may be shown and edited.</returns>
    public static bool IsShown(CustomField field, int index, IReadOnlyList<SecretFinding> findings)
    {
        ArgumentNullException.ThrowIfNull(field);
        ArgumentNullException.ThrowIfNull(findings);

        return field.Type == CustomFieldType.Text
            && !IsAdditionalFolders(field)
            && !findings.Any(finding => SecretLocation.TryParseField(finding.Location.FieldPath, out int inField, out _) && inField == index);
    }

    /// <summary>Whether a custom field is the additional folders field, which is shown as a list of folders.</summary>
    /// <param name="field">Custom field.</param>
    /// <returns><see langword="true"/> for a text field named <see cref="AdditionalFolders.FieldName"/>.</returns>
    public static bool IsAdditionalFolders(CustomField field)
    {
        ArgumentNullException.ThrowIfNull(field);

        return field.Type == CustomFieldType.Text && string.Equals(field.Name, AdditionalFolders.FieldName, StringComparison.Ordinal);
    }

    /// <summary>Masks the user info, the query, and the fragment of a website address with <see cref="Mask"/>.</summary>
    /// <param name="address">Address as stored; it need not be a valid absolute URI.</param>
    /// <returns>The address as it may be shown.</returns>
    public static string MaskUri(string address)
    {
        ArgumentNullException.ThrowIfNull(address);

        return UriMasking.Mask(address, Mask);
    }

    /// <summary>Whether a website address holds user info, a query, or a fragment, which <see cref="MaskUri"/> masks.</summary>
    /// <param name="address">Address as stored.</param>
    /// <returns><see langword="true"/> when the address is shown masked.</returns>
    public static bool IsMasked(string address) => !string.Equals(MaskUri(address), address, StringComparison.Ordinal);

    /// <summary>The user name of a login, unless a secret finding lies in it.</summary>
    /// <param name="item">Item.</param>
    /// <param name="findings">Secret findings of the item.</param>
    /// <returns>The user name, or <see langword="null"/> when the item has none or a finding lies at <c>login.username</c>.</returns>
    public static string? Username(VaultItem item, IReadOnlyList<SecretFinding> findings)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(findings);

        return findings.Any(finding => string.Equals(finding.Location.FieldPath, SecretLocation.LoginUsername, StringComparison.Ordinal))
            ? null
            : item.Login?.Username;
    }
}
