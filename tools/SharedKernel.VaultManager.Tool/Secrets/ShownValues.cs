using PandaBear.VaultManager.Tool.Model;

namespace PandaBear.VaultManager.Tool.Secrets;

/// <summary>
/// Which values of an item may be shown to a person or an agent who must not see its secrets, as the catalog and the
/// merge brief show them: text custom fields without a secret finding, website addresses with their user info, query, and
/// fragment masked, and user names without a secret finding. The scanner checks user names only for misfiled TOTP keys
/// and never scans addresses, so their text is scanned here too, as notes are.
/// </summary>
internal static class ShownValues
{
    /// <summary>Text that replaces the masked parts of a website address.</summary>
    public const string Mask = "***";

    // Field path under which the text of an address is scanned; its findings are only counted.
    private const string AddressPath = "login.uris";

    /// <summary>
    /// Whether a custom field may be shown: a text field that is not the additional folders field and in whose name and
    /// value no secret finding lies.
    /// </summary>
    /// <param name="field">Custom field.</param>
    /// <param name="index">Position of the field in the item.</param>
    /// <param name="findings">Secret findings of the item.</param>
    /// <returns><see langword="true"/> when the field may be shown and edited.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="field"/> or <paramref name="findings"/> is <see langword="null"/>.</exception>
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
    /// <exception cref="ArgumentNullException"><paramref name="field"/> is <see langword="null"/>.</exception>
    public static bool IsAdditionalFolders(CustomField field)
    {
        ArgumentNullException.ThrowIfNull(field);

        return field.Type == CustomFieldType.Text && string.Equals(field.Name, AdditionalFolders.FieldName, StringComparison.Ordinal);
    }

    /// <summary>
    /// Masks the user info, the query, and the fragment of a website address with <see cref="Mask"/>, and everything after
    /// the scheme when the scanner finds a secret in what is left, such as a webhook path or a connection string stored as
    /// an address.
    /// </summary>
    /// <param name="address">Address as stored; it need not be a valid absolute URI.</param>
    /// <returns>The address as it may be shown.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="address"/> is <see langword="null"/>.</exception>
    public static string MaskUri(string address)
    {
        ArgumentNullException.ThrowIfNull(address);

        string masked = UriMasking.Mask(address, Mask);
        return HoldsSecret(masked, AddressPath) ? UriMasking.MaskAfterScheme(address, Mask) : masked;
    }

    /// <summary>
    /// Whether <see cref="MaskUri"/> masks part of a website address: it holds user info, a query, a fragment, or a secret
    /// finding.
    /// </summary>
    /// <param name="address">Address as stored.</param>
    /// <returns><see langword="true"/> when the address is shown masked.</returns>
    public static bool IsMasked(string address) => !string.Equals(MaskUri(address), address, StringComparison.Ordinal);

    /// <summary>The user name of a login, unless a secret finding lies in it.</summary>
    /// <param name="item">Item.</param>
    /// <param name="findings">Secret findings of the item.</param>
    /// <returns>
    /// The user name, or <see langword="null"/> when the item has none, a finding lies at <c>login.username</c>, or the scanner
    /// finds a secret in its text, such as a token stored as the user name.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="item"/> or <paramref name="findings"/> is <see langword="null"/>.</exception>
    public static string? Username(VaultItem item, IReadOnlyList<SecretFinding> findings)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(findings);

        string? username = item.Login?.Username;
        return username is null
            || findings.Any(finding => string.Equals(finding.Location.FieldPath, SecretLocation.LoginUsername, StringComparison.Ordinal))
            || HoldsSecret(username, SecretLocation.LoginUsername)
                ? null
                : username;
    }

    private static bool HoldsSecret(string text, string fieldPath) => SecretScanner.Default.ScanText(text, fieldPath, null).Count > 0;
}
