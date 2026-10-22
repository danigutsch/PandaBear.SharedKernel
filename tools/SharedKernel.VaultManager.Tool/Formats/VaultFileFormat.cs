namespace PandaBear.VaultManager.Tool.Formats;

/// <summary>Vault export formats the tool reads.</summary>
internal enum VaultFileFormat
{
    /// <summary>Content matched no supported format.</summary>
    Unknown = 0,

    /// <summary>Keeper JSON from the web vault or Commander.</summary>
    KeeperJson,

    /// <summary>Keeper Commander zip with <c>export.json</c> and attachments.</summary>
    KeeperJsonZip,

    /// <summary>Keeper CSV from the web vault or Commander.</summary>
    KeeperCsv,

    /// <summary>Unencrypted Bitwarden JSON.</summary>
    BitwardenJson,

    /// <summary>Bitwarden JSON encrypted with a file password.</summary>
    BitwardenJsonPasswordProtected,

    /// <summary>Bitwarden JSON encrypted with the account key; it cannot be read offline.</summary>
    BitwardenJsonAccountRestricted,

    /// <summary>Bitwarden CSV, individual or organization.</summary>
    BitwardenCsv,

    /// <summary>Bitwarden zip with <c>data.json</c> and attachments.</summary>
    BitwardenZip,
}
