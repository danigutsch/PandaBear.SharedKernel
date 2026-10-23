using PandaBear.VaultManager.Tool.Model;

namespace PandaBear.VaultManager.Tool.Catalog;

/// <summary>What a catalog item holds besides its editable parts. <c>apply</c> ignores it.</summary>
/// <param name="Type">Item type.</param>
/// <param name="Username">User name of a login, or <see langword="null"/> when it has none or a secret finding lies in it.</param>
/// <param name="HasPassword">Whether the login has a password.</param>
/// <param name="HasTotp">Whether the login has a TOTP.</param>
/// <param name="Passkeys">Number of passkeys of the login.</param>
/// <param name="PasswordHistory">Number of previous passwords.</param>
/// <param name="ProtectedFields">
/// Names of the custom fields that the catalog does not show: hidden, boolean, and linked fields, and text fields that
/// hold a secret finding. <c>apply</c> keeps them as they are.
/// </param>
/// <param name="Attachments">Attachments of the item.</param>
internal sealed record CatalogInfo(
    VaultItemType Type,
    string? Username,
    bool HasPassword,
    bool HasTotp,
    int Passkeys,
    int PasswordHistory,
    IReadOnlyList<string> ProtectedFields,
    IReadOnlyList<CatalogAttachment> Attachments);
