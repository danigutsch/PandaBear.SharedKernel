namespace PandaBear.VaultManager.Tool.Merge;

/// <summary>
/// Paths that name item fields in decisions, conflicts, and reports. They follow Bitwarden export property names.
/// </summary>
internal static class FieldPaths
{
    /// <summary>Item type.</summary>
    public const string Type = "type";

    /// <summary>Item name.</summary>
    public const string Name = "name";

    /// <summary>Folder path.</summary>
    public const string Folder = "folder";

    /// <summary>Notes.</summary>
    public const string Notes = "notes";

    /// <summary>Favorite flag.</summary>
    public const string Favorite = "favorite";

    /// <summary>Master password reprompt flag.</summary>
    public const string Reprompt = "reprompt";

    /// <summary>Login user name.</summary>
    public const string LoginUsername = "login.username";

    /// <summary>Login password.</summary>
    public const string LoginPassword = "login.password";

    /// <summary>Login authenticator key.</summary>
    public const string LoginTotp = "login.totp";

    /// <summary>Login website addresses.</summary>
    public const string LoginUris = "login.uris";

    /// <summary>Login passkeys.</summary>
    public const string LoginFido2Credentials = "login.fido2Credentials";

    /// <summary>Custom fields.</summary>
    public const string Fields = "fields";

    /// <summary>Password history.</summary>
    public const string PasswordHistory = "passwordHistory";

    /// <summary>Prefix of card data paths, followed by the camelCase property name.</summary>
    public const string CardPrefix = "card.";

    /// <summary>Prefix of identity data paths, followed by the camelCase property name.</summary>
    public const string IdentityPrefix = "identity.";

    /// <summary>Prefix of SSH key data paths, followed by the camelCase property name.</summary>
    public const string SshKeyPrefix = "sshKey.";
}
