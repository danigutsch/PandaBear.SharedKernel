using PandaBear.VaultManager.Tool.Catalog;
using PandaBear.VaultManager.Tool.Matching;
using PandaBear.VaultManager.Tool.Merge;
using PandaBear.VaultManager.Tool.Model;
using PandaBear.VaultManager.Tool.Pipeline;
using PandaBear.VaultManager.Tool.Tests.Pipeline;

namespace PandaBear.VaultManager.Tool.Tests.Catalog;

internal static class CatalogFixture
{
    public const string Password = "fixture-catalog-password";
    public const string HiddenValue = "fixture-hidden-api-key";
    public const string NotesText = "fixture private notes text";
    public const string MaskedUri = "https://alice:fixture-uri-secret@example.com/reset?token=fixture-uri-token";
    public const string Key = "bitwarden|vault.json|item-1";
    public static readonly string Totp = PipelineSecrets.TotpUri(PipelineSecrets.TotpSecret(2100));
    public static readonly string SeedInField = PipelineSecrets.TotpUri(PipelineSecrets.TotpSecret(2110));

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

    public static LoadedVault Vault(params VaultItem[] items)
    {
        IReadOnlyList<string> keys = OriginKeys.Compute(items);
        return new LoadedVault(["vault.json"], [.. items.Select((item, index) => new SourceItem(item, keys[index], "vault.json"))], [])
        {
            Folders = ["Archive"],
        };
    }

    public static CatalogDocument CatalogWith(LoadedVault vault, IReadOnlyList<CatalogItem> items, IReadOnlyList<string>? folders = null)
    {
        CatalogDocument catalog = CatalogBuilder.Build(vault, "abc", MergeDecisions.None).Catalog;
        return catalog with
        {
            Items = items,
            Folders = folders ?? catalog.Folders
        };
    }

    public static MergeDecisions Notes(NotesDecision decision) =>
        MergeDecisions.None with
        {
            Notes = [new ItemNotesDecision(Key, decision, NotesSha256: decision == NotesDecision.Share ? ItemNotesDecision.HashOf(NotesText) : null)],
        };
}
