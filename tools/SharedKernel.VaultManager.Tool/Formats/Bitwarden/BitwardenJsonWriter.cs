using System.Buffers;
using System.Globalization;
using System.Text;
using System.Text.Json;
using PandaBear.VaultManager.Tool.Model;

namespace PandaBear.VaultManager.Tool.Formats.Bitwarden;

/// <summary>Writes unencrypted Bitwarden JSON exports that Bitwarden and Vaultwarden can import.</summary>
internal static class BitwardenJsonWriter
{
    /// <summary>
    /// Longest custom field name that errors quote. A longer name, such as pasted text or a long security question, is
    /// left out, and the field is named by its position on the item.
    /// </summary>
    public const int MaxQuotedFieldNameLength = 40;

    // A client sends the encrypted SHA-256 checksum of each login URI with it: the 44 base64 characters of the hash as an
    // encrypted string, in a "uriChecksum" property.
    private static readonly long UriChecksumBytes = BitwardenLimits.EncryptedLength(new string('=', 44)) + ",\"uriChecksum\":\"\"".Length;

    /// <summary>
    /// Writes items as an unencrypted individual-vault Bitwarden export. Its folders are the declared folders, the folders
    /// of the items, and the parent of each, so that Bitwarden nests them (<see cref="FolderTree.WithParents"/>).
    /// </summary>
    /// <param name="export">Items to write.</param>
    /// <returns>
    /// Export JSON and problems that would affect the import. Values are never truncated, and problems name custom fields
    /// by position, quoting only names of at most <see cref="MaxQuotedFieldNameLength"/> characters. A warning reports an
    /// estimated import request larger than <see cref="BitwardenLimits.VaultwardenImportBytes"/>: the export as compact
    /// JSON, with every value and folder name as an encrypted string and an encrypted checksum for every login URI, as a
    /// Bitwarden client that encrypts each field on its own sends it. Another warning names each login that holds more
    /// than one passkey.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="export"/> is <see langword="null"/>.</exception>
    public static BitwardenWriteResult Write(VaultExport export)
    {
        ArgumentNullException.ThrowIfNull(export);

        var errors = new List<string>();
        var warnings = new List<string>();
        IReadOnlyList<string> folderPaths = FolderTree.WithParents(export.Folders.Concat(export.Items.Select(item => item.FolderPath)));
        Dictionary<string, string> folderIds = folderPaths.ToDictionary(path => path, _ => Guid.NewGuid().ToString(), StringComparer.Ordinal);
        var items = new List<BitwardenItemDto>(export.Items.Count);

        // Bytes the import request adds to the compact export JSON.
        long requestGrowth = 0;
        foreach (VaultItem item in export.Items)
        {
            VaultItem prepared = MoveHistoryOverflowToFields(item, warnings);
            CheckLimits(prepared, errors);
            items.Add(ToDto(prepared, folderIds));
            requestGrowth += EncryptedValues(prepared).Sum(EncryptionGrowth);
            if (prepared.Type == VaultItemType.Login && prepared.Login is { } login)
            {
                requestGrowth += login.Uris.Count * UriChecksumBytes;
                WarnAboutPasskeys(prepared.Name, login, warnings);
            }
        }

        var document = new BitwardenExportDto
        {
            Encrypted = false,
            Folders = [.. folderPaths.Select(path => new BitwardenFolderDto(folderIds[path], path))],
            Items = items,
        };
        string json = JsonSerializer.Serialize(document, BitwardenJsonContext.Default.BitwardenExportDto);

        // The client posts compact JSON and sends each value as an encrypted string, which adds the IV, the MAC, the
        // padding, and base64 to it: about 95 bytes for a short value. Folder names are encrypted too.
        long requestBytes = CompactLength(document) + requestGrowth + folderIds.Keys.Sum(EncryptionGrowth);
        if (requestBytes > BitwardenLimits.VaultwardenImportBytes)
        {
            warnings.Add(string.Create(
                CultureInfo.InvariantCulture,
                $"The import request is estimated at {requestBytes:N0} bytes once a Bitwarden client encrypts each value, more than the {BitwardenLimits.VaultwardenImportBytes:N0} bytes Vaultwarden accepts in one request, so Vaultwarden may reject the import."));
        }

        return new BitwardenWriteResult(json, errors, warnings);
    }

    // Bytes a value gains in the import request, where it is an encrypted string instead of its JSON text.
    private static long EncryptionGrowth(string value) => BitwardenLimits.EncryptedLength(value) - Encoding.UTF8.GetByteCount(value);

    // UTF-8 length of the document without indentation. A writer of its own decides the layout, so the indented export
    // options of the context do not apply.
    private static long CompactLength(BitwardenExportDto document)
    {
        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            JsonSerializer.Serialize(writer, document, BitwardenJsonContext.Default.BitwardenExportDto);
        }

        return buffer.WrittenCount;
    }

    // The values the client encrypts, as ToDto writes them: the item type decides which typed data is written.
    private static IEnumerable<string> EncryptedValues(VaultItem item)
    {
        List<string?> values = [item.Name, item.Notes];
        values.AddRange(item.Fields.SelectMany(field => new[] { field.Name, field.Value }));
        values.AddRange(item.PasswordHistory.Select(entry => entry.Password));
        switch (item.Type)
        {
            case VaultItemType.Login when item.Login is { } login:
                values.AddRange([login.Username, login.Password, login.Totp]);
                values.AddRange(login.Uris.Select(uri => uri.Value));
                values.AddRange(login.Fido2Credentials.SelectMany(PasskeyValues));
                break;
            case VaultItemType.Card when item.Card is { } card:
                values.AddRange([card.CardholderName, card.Brand, card.Number, card.ExpMonth, card.ExpYear, card.Code]);
                break;
            case VaultItemType.Identity when item.Identity is { } identity:
                values.AddRange(
                [
                    identity.Title, identity.FirstName, identity.MiddleName, identity.LastName, identity.Address1, identity.Address2,
                    identity.Address3, identity.City, identity.State, identity.PostalCode, identity.Country, identity.Company,
                    identity.Email, identity.Phone, identity.Ssn, identity.Username, identity.PassportNumber, identity.LicenseNumber,
                ]);
                break;
            case VaultItemType.SshKey when item.SshKey is { } key:
                values.AddRange([key.PrivateKey, key.PublicKey, key.KeyFingerprint]);
                break;
            default:
                break;
        }

        return values.OfType<string>();
    }

    private static string?[] PasskeyValues(Fido2Credential passkey) =>
    [
        passkey.CredentialId, passkey.KeyType, passkey.KeyAlgorithm, passkey.KeyCurve, passkey.KeyValue, passkey.RpId,
        passkey.UserHandle, passkey.UserName, passkey.Counter, passkey.RpName, passkey.UserDisplayName, passkey.Discoverable,
    ];

    // Bitwarden saves one passkey per login, and whether a client keeps the others is not verified.
    private static void WarnAboutPasskeys(string name, LoginData login, List<string> warnings)
    {
        if (login.Fido2Credentials.Count > 1)
        {
            warnings.Add(string.Create(
                CultureInfo.InvariantCulture,
                $"\"{name}\": {login.Fido2Credentials.Count} passkeys on one login. Bitwarden saves one passkey per login, so a client can drop or hide the others; to keep each passkey on a login of its own, split the item's group in the review or in the decisions file."));
        }
    }

    private static VaultItem MoveHistoryOverflowToFields(VaultItem item, List<string> warnings)
    {
        if (item.PasswordHistory.Count <= BitwardenLimits.PasswordHistoryEntries)
        {
            return item;
        }

        PasswordHistoryEntry[] ordered = [.. item.PasswordHistory.OrderByDescending(entry => entry.LastUsedDate)];
        IEnumerable<CustomField> overflow = ordered.Skip(BitwardenLimits.PasswordHistoryEntries).Select(entry =>
            new CustomField(
                string.Create(CultureInfo.InvariantCulture, $"Previous password ({entry.LastUsedDate:yyyy-MM-dd})"),
                entry.Password,
                CustomFieldType.Hidden));
        warnings.Add($"\"{item.Name}\": password history beyond {BitwardenLimits.PasswordHistoryEntries} entries moved to hidden fields.");
        return item with
        {
            PasswordHistory = [.. ordered.Take(BitwardenLimits.PasswordHistoryEntries)],
            Fields = [.. item.Fields, .. overflow],
        };
    }

    private static void CheckLimits(VaultItem item, List<string> errors)
    {
        void Check(string? value, int limit, string what)
        {
            if (!BitwardenLimits.Fits(value, limit))
            {
                errors.Add($"\"{item.Name}\": {what} is too long for Bitwarden import (encrypted limit {limit}).");
            }
        }

        Check(item.Name, BitwardenLimits.ShortEncryptedLength, "name");
        Check(item.Notes, BitwardenLimits.NotesEncryptedLength, "notes");
        Check(item.Login?.Password, BitwardenLimits.ValueEncryptedLength, "password");
        Check(item.Login?.Totp, BitwardenLimits.ShortEncryptedLength, "TOTP");
        Check(item.SshKey?.PrivateKey, BitwardenLimits.ValueEncryptedLength, "SSH private key");
        Check(item.SshKey?.PublicKey, BitwardenLimits.ValueEncryptedLength, "SSH public key");
        for (int index = 0; index < item.Fields.Count; index++)
        {
            CustomField field = item.Fields[index];
            string position = string.Create(CultureInfo.InvariantCulture, $"custom field {index + 1}");
            Check(field.Name, BitwardenLimits.ShortEncryptedLength, "the name of " + position);
            Check(field.Value, BitwardenLimits.ValueEncryptedLength, "the value of " + FieldLabel(position, field.Name));
        }
    }

    private static string FieldLabel(string position, string? name) =>
        string.IsNullOrWhiteSpace(name) || name.Length > MaxQuotedFieldNameLength ? position : position + " \"" + name + "\"";

    private static BitwardenItemDto ToDto(VaultItem item, Dictionary<string, string> folderIds)
    {
        string? folderId = string.IsNullOrWhiteSpace(item.FolderPath) ? null : folderIds[item.FolderPath];

        return new BitwardenItemDto
        {
            Id = Guid.NewGuid().ToString(),
            FolderId = folderId,
            Type = (int)item.Type,
            Reprompt = item.Reprompt ? 1 : 0,
            Name = item.Name,
            Notes = item.Notes,
            Favorite = item.Favorite,
            Fields = item.Fields.Count > 0 ? item.Fields : null,
            Login = item.Type == VaultItemType.Login ? ToDto(item.Login ?? new LoginData()) : null,
            SecureNote = item.Type == VaultItemType.SecureNote ? new BitwardenSecureNoteDto(0) : null,
            Card = item.Type == VaultItemType.Card ? item.Card ?? new CardData() : null,
            Identity = item.Type == VaultItemType.Identity ? item.Identity ?? new IdentityData() : null,
            SshKey = item.Type == VaultItemType.SshKey ? item.SshKey ?? new SshKeyData() : null,
            PasswordHistory = item.PasswordHistory.Count > 0 ? item.PasswordHistory : null,
            RevisionDate = item.RevisionDate,
            CreationDate = item.CreationDate,
        };
    }

    private static BitwardenLoginDto ToDto(LoginData login)
    {
        return new BitwardenLoginDto
        {
            Uris = [.. login.Uris.Select(uri => new BitwardenUriDto((int?)uri.Match, uri.Value))],
            Username = login.Username,
            Password = login.Password,
            Totp = login.Totp,
            Fido2Credentials = login.Fido2Credentials,
        };
    }
}
