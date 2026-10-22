using System.Globalization;
using System.Text;
using System.Text.Json;
using PandaBear.VaultManager.Tool.Model;

namespace PandaBear.VaultManager.Tool.Formats.Bitwarden;

/// <summary>Writes unencrypted Bitwarden JSON exports that Bitwarden and Vaultwarden can import.</summary>
internal static class BitwardenJsonWriter
{
    /// <summary>Writes items as an unencrypted individual-vault Bitwarden export.</summary>
    /// <param name="export">Items to write.</param>
    /// <returns>Export JSON and problems that would affect the import. Values are never truncated.</returns>
    public static BitwardenWriteResult Write(VaultExport export)
    {
        ArgumentNullException.ThrowIfNull(export);

        var errors = new List<string>();
        var warnings = new List<string>();
        var folderIds = new Dictionary<string, string>(StringComparer.Ordinal);
        var items = new List<BitwardenItemDto>(export.Items.Count);
        foreach (VaultItem item in export.Items)
        {
            VaultItem prepared = MoveHistoryOverflowToFields(item, warnings);
            CheckLimits(prepared, errors);
            items.Add(ToDto(prepared, folderIds));
        }

        var document = new BitwardenExportDto
        {
            Encrypted = false,
            Folders = [.. folderIds.Select(folder => new BitwardenFolderDto(folder.Value, folder.Key))],
            Items = items,
        };
        string json = JsonSerializer.Serialize(document, BitwardenJsonContext.Default.BitwardenExportDto);

        // Encryption grows each value by about a third; warn before the server-side request limit is reached.
        if (Encoding.UTF8.GetByteCount(json) * 4L / 3 > BitwardenLimits.VaultwardenImportBytes)
        {
            warnings.Add("Export is close to or above Vaultwarden's 20 MB import limit; split it into several imports.");
        }

        return new BitwardenWriteResult(json, errors, warnings);
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
        foreach (CustomField field in item.Fields)
        {
            Check(field.Name, BitwardenLimits.ShortEncryptedLength, $"field name \"{field.Name}\"");
            Check(field.Value, BitwardenLimits.ValueEncryptedLength, $"field \"{field.Name}\"");
        }
    }

    private static BitwardenItemDto ToDto(VaultItem item, Dictionary<string, string> folderIds)
    {
        string? folderId = null;
        if (!string.IsNullOrWhiteSpace(item.FolderPath) && !folderIds.TryGetValue(item.FolderPath, out folderId))
        {
            folderId = Guid.NewGuid().ToString();
            folderIds.Add(item.FolderPath, folderId);
        }

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
