namespace PandaBear.VaultManager.Tool.Attachments;

/// <summary>Content kind of an attachment, detected by magic bytes first and file extension second.</summary>
internal enum AttachmentKind
{
    /// <summary>Content that matched no other kind.</summary>
    Binary = 0,

    /// <summary>Valid UTF-8 without NUL characters.</summary>
    Text,

    /// <summary>Text holding a PEM, OpenSSH, PGP, PuTTY, or SSH2 private key.</summary>
    PrivateKeyText,

    /// <summary>KeePass database.</summary>
    KeePass,

    /// <summary>PKCS#12 key store, detected on a best-effort basis.</summary>
    Pkcs12,

    /// <summary>Zip archive.</summary>
    Zip,

    /// <summary>SQLite database.</summary>
    Sqlite,

    /// <summary>Berkeley DB file, for example a Bitcoin Core <c>wallet.dat</c>.</summary>
    BerkeleyDb,

    /// <summary>Ethereum version 3 keystore JSON.</summary>
    EthereumKeystore,

    /// <summary>PDF document.</summary>
    Pdf,
}
