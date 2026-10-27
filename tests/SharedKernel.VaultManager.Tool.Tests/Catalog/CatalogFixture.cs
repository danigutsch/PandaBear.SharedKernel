using PandaBear.VaultManager.Tool.Catalog;
using PandaBear.VaultManager.Tool.Matching;
using PandaBear.VaultManager.Tool.Merge;
using PandaBear.VaultManager.Tool.Model;
using PandaBear.VaultManager.Tool.Pipeline;
using PandaBear.VaultManager.Tool.Tests.Pipeline;

namespace PandaBear.VaultManager.Tool.Tests.Catalog;

/// <summary>Builds a vault item with many kinds of secret and metadata, and the vault, catalog, and decisions around it.</summary>
internal static class CatalogFixture
{
    /// <summary>The password of the sample login.</summary>
    public const string Password = "fixture-catalog-password";
    /// <summary>The value of a hidden custom field of the sample login.</summary>
    public const string HiddenValue = "fixture-hidden-api-key";
    /// <summary>The notes of the sample login.</summary>
    public const string NotesText = "fixture private notes text";
    /// <summary>An address of the sample login that holds credentials and a token.</summary>
    public const string MaskedUri = "https://alice:fixture-uri-secret@example.com/reset?token=fixture-uri-token";
    /// <summary>The origin key of the sample login.</summary>
    public const string Key = "bitwarden|vault.json|item-1";
    /// <summary>The one-time password URI of the sample login.</summary>
    public static readonly string Totp = PipelineSecrets.TotpUri(PipelineSecrets.TotpSecret(2100));
    /// <summary>A one-time password URI stored in a text field of the sample login.</summary>
    public static readonly string SeedInField = PipelineSecrets.TotpUri(PipelineSecrets.TotpSecret(2110));

    /// <summary>Creates the sample login with notes, custom fields, two addresses, a one-time password, and a password history.</summary>
    public static VaultItem Login() => new()
    {
        Type = VaultItemType.Login,
        Name = "GitHub",
        FolderPath = "Work",
        Notes = NotesText,
        Fields =
        [
            new CustomField("Plan", "Team", CustomFieldType.Text),
            new CustomField("API key", HiddenValue, CustomFieldType.Hidden),
            new CustomField("Backup", SeedInField, CustomFieldType.Text),
            new CustomField(AdditionalFolders.FieldName, "Personal", CustomFieldType.Text),
        ],
        Login = new LoginData
        {
            Username = "alice",
            Password = Password,
            Totp = Totp,
            Uris = [new LoginUri("https://github.com/login"), new LoginUri(MaskedUri)],
        },
        PasswordHistory = [new PasswordHistoryEntry("fixture-catalog-old-password", DateTimeOffset.UnixEpoch)],
        Origin = new ItemOrigin("bitwarden", "vault.json", "item-1", null),
    };

    /// <summary>Wraps the items in a loaded vault with one empty folder, <c>Archive</c>.</summary>
    public static LoadedVault Vault(params VaultItem[] items)
    {
        IReadOnlyList<string> keys = OriginKeys.Compute(items);
        return new LoadedVault(["vault.json"], [.. items.Select((item, index) => new SourceItem(item, keys[index], "vault.json"))], [])
        {
            Folders = ["Archive"],
        };
    }

    /// <summary>Builds a catalog of the vault and replaces its items, and optionally its folders.</summary>
    public static CatalogDocument CatalogWith(LoadedVault vault, IReadOnlyList<CatalogItem> items, IReadOnlyList<string>? folders = null)
    {
        CatalogDocument catalog = CatalogBuilder.Build(vault, "abc", MergeDecisions.None).Catalog;
        return catalog with
        {
            Items = items,
            Folders = folders ?? catalog.Folders
        };
    }

    /// <summary>Creates decisions that give the sample login's notes the decision, with the notes hash when the decision shares them.</summary>
    public static MergeDecisions Notes(NotesDecision decision) =>
        MergeDecisions.None with
        {
            Notes = [new ItemNotesDecision(Key, decision, NotesSha256: decision == NotesDecision.Share ? ItemNotesDecision.HashOf(NotesText) : null)],
        };
}
