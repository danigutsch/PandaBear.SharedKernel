using PandaBear.VaultManager.Tool.Matching;
using PandaBear.VaultManager.Tool.Model;
using PandaBear.VaultManager.Tool.Secrets;

namespace PandaBear.VaultManager.Tool.Pipeline;

/// <summary>
/// Completes SSH key items for Bitwarden, which stores the private key, public key, and fingerprint together. An item
/// missing its public key or fingerprint is converted with <see cref="SshKeyConverter"/>. When conversion fails, the item
/// becomes a secure note that keeps the private key in a hidden field, so a key is never dropped.
/// </summary>
/// <remarks>
/// Bitwarden holds one key per item, so only the first PEM block of the stored private key text is converted. Any other
/// text stored with it, such as a second key or a host name, moves to the hidden field <see cref="OtherTextField"/>,
/// where secret detection finds further keys. That field keeps the content of the text before and after the block, not
/// its exact layout: CR LF line breaks become line feeds, whitespace at the start and end of each part is dropped, and
/// when both parts hold text they are joined by one line feed.
/// </remarks>
internal static class SshKeyNormalizer
{
    /// <summary>Name of the hidden field that keeps a private key that could not be converted.</summary>
    public const string PrivateKeyField = "Private key";

    /// <summary>Name of the hidden field that keeps the text stored with a converted private key besides its first PEM block.</summary>
    public const string OtherTextField = "Private key (other text)";

    /// <summary>Name of the field that keeps a public key that could not be used.</summary>
    public const string PublicKeyField = "Public key";

    /// <summary>Name of the field that keeps a fingerprint that could not be used.</summary>
    public const string FingerprintField = "Key fingerprint";

    /// <summary>Suffix of fields that keep a stored value replaced by one derived from the private key.</summary>
    public const string SourceSuffix = " (from source)";

    private static readonly char[] Separators = [' ', '\t'];

    /// <summary>Completes an SSH key item. Other items, and SSH key items that are complete, are returned unchanged.</summary>
    /// <param name="item">Item.</param>
    /// <returns>The completed item, or a secure note that keeps the key when it cannot be converted.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="item"/> is <see langword="null"/>.</exception>
    public static VaultItem Normalize(VaultItem item)
    {
        ArgumentNullException.ThrowIfNull(item);

        if (item.Type != VaultItemType.SshKey)
        {
            return item;
        }

        SshKeyData key = item.SshKey ?? new SshKeyData();
        if (!ItemNormalizer.IsAbsent(key.PublicKey) && !ItemNormalizer.IsAbsent(key.KeyFingerprint))
        {
            return item;
        }

        if (ItemNormalizer.IsAbsent(key.PrivateKey))
        {
            return ToSecureNote(item, key, "SSH key item has no private key, so it is a secure note; its public key and fingerprint are kept as fields.");
        }

        (string firstKey, string? otherText) = SplitFirstBlock(key.PrivateKey!);
        SshKeyConversion conversion = SshKeyConverter.Convert(firstKey, key.PublicKey);
        return conversion.Key is { } converted
            ? Complete(item, key, converted, firstKey, otherText)
            : ToSecureNote(item, key, $"SSH key was not converted ({conversion.Problem}), so the item is a secure note with the private key in the hidden field \"{PrivateKeyField}\".");
    }

    // Returns the first PEM block, with line feeds and without surrounding whitespace, and the text before and after it.
    // Text without a complete PEM block is returned whole, so the converter reports why it cannot be converted.
    private static (string FirstKey, string? OtherText) SplitFirstBlock(string stored)
    {
        string text = stored.Replace("\r\n", "\n", StringComparison.Ordinal).Trim();
        if (!PemBlock.TryParse(text, out _, out Range extent))
        {
            return (stored, null);
        }

        string before = text[..extent.Start].Trim();
        string after = text[extent.End..].Trim();
        string other = before.Length > 0 && after.Length > 0 ? before + "\n" + after : before + after;
        return (text[extent], other.Length == 0 ? null : other);
    }

    private static VaultItem Complete(VaultItem item, SshKeyData stored, SshKeyData converted, string firstKey, string? otherText)
    {
        var fields = new List<CustomField>(item.Fields);
        var notes = new List<string>(item.ConversionNotes);
        string publicKey = converted.PublicKey!;
        if (Present(stored.PublicKey) is { } storedPublicKey)
        {
            if (NamesSameKey(storedPublicKey, publicKey))
            {
                // Keep the stored form, which may carry a comment.
                publicKey = storedPublicKey;
            }
            else
            {
                fields.Add(new CustomField(PublicKeyField + SourceSuffix, storedPublicKey, CustomFieldType.Text));
                notes.Add($"The stored SSH public key does not match the private key; the item uses the public key derived from the private key, and the stored one is in the field \"{PublicKeyField + SourceSuffix}\".");
            }
        }

        if (Present(stored.KeyFingerprint) is { } storedFingerprint && !string.Equals(storedFingerprint, converted.KeyFingerprint, StringComparison.Ordinal))
        {
            fields.Add(new CustomField(FingerprintField + SourceSuffix, storedFingerprint, CustomFieldType.Text));
            notes.Add($"The stored SSH key fingerprint differs from the SHA256 fingerprint of the private key; the stored one is in the field \"{FingerprintField + SourceSuffix}\".");
        }

        if (!string.Equals(converted.PrivateKey, firstKey, StringComparison.Ordinal))
        {
            notes.Add("The SSH private key was re-encoded as PKCS#8, a form Bitwarden accepts.");
        }

        if (otherText is not null)
        {
            fields.Add(new CustomField(OtherTextField, otherText, CustomFieldType.Hidden));
            notes.Add($"The SSH private key field holds text besides its first key, such as another key; the item uses the first key, and the other text is in the hidden field \"{OtherTextField}\".");
        }

        return item with
        {
            SshKey = new SshKeyData { PrivateKey = converted.PrivateKey, PublicKey = publicKey, KeyFingerprint = converted.KeyFingerprint },
            Fields = fields,
            ConversionNotes = notes,
        };
    }

    private static VaultItem ToSecureNote(VaultItem item, SshKeyData key, string note)
    {
        var fields = new List<CustomField>(item.Fields);
        AddIfPresent(fields, PrivateKeyField, key.PrivateKey, CustomFieldType.Hidden);
        AddIfPresent(fields, PublicKeyField, key.PublicKey, CustomFieldType.Text);
        AddIfPresent(fields, FingerprintField, key.KeyFingerprint, CustomFieldType.Text);
        return item with
        {
            Type = VaultItemType.SecureNote,
            SshKey = null,
            Fields = fields,
            ConversionNotes = [.. item.ConversionNotes, note],
        };
    }

    // Public keys read "type base64 [comment]"; the same type and key blob name the same key.
    private static bool NamesSameKey(string stored, string derived)
    {
        string[] storedParts = stored.Split(Separators, 3, StringSplitOptions.RemoveEmptyEntries);
        string[] derivedParts = derived.Split(Separators, 3, StringSplitOptions.RemoveEmptyEntries);
        return storedParts.Length >= 2 && derivedParts.Length >= 2
            && string.Equals(storedParts[0], derivedParts[0], StringComparison.Ordinal)
            && string.Equals(storedParts[1], derivedParts[1], StringComparison.Ordinal);
    }

    private static void AddIfPresent(List<CustomField> fields, string name, string? value, CustomFieldType type)
    {
        if (!ItemNormalizer.IsAbsent(value))
        {
            fields.Add(new CustomField(name, value, type));
        }
    }

    private static string? Present(string? value) => ItemNormalizer.IsAbsent(value) ? null : value!.Trim();
}
