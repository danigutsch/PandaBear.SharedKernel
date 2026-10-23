namespace PandaBear.VaultManager.Tool.Secrets;

/// <summary>Kind of secret a finding refers to.</summary>
internal enum SecretKind
{
    /// <summary>Time-based one-time password seed or URI.</summary>
    Totp = 0,

    /// <summary>Block of two-factor recovery codes, or a single recovery key.</summary>
    RecoveryCodes,

    /// <summary>Bitwarden account recovery code, which must not live in the vault it protects.</summary>
    BitwardenRecoveryCode,

    /// <summary>Security question and its answer.</summary>
    SecurityQuestion,

    /// <summary>Personal identification number.</summary>
    Pin,

    /// <summary>Password written as a labeled line.</summary>
    Password,

    /// <summary>User name written as a labeled line.</summary>
    Username,

    /// <summary>API token or secret key.</summary>
    ApiToken,

    /// <summary>Public identifier paired with a secret, such as an access key ID.</summary>
    AccessKeyId,

    /// <summary>PEM or OpenSSH private key.</summary>
    PrivateKey,

    /// <summary>OpenPGP private key block.</summary>
    PgpPrivateKey,

    /// <summary>PuTTY private key file.</summary>
    PuttyKey,

    /// <summary>BIP39 or Electrum seed phrase.</summary>
    SeedPhrase,

    /// <summary>Payment card number.</summary>
    CardNumber,

    /// <summary>International bank account number or bank identifier code.</summary>
    Iban,

    /// <summary>Database URI or connection string with a password.</summary>
    ConnectionString,

    /// <summary>URL with embedded credentials or a signature.</summary>
    UrlCredentials,

    /// <summary>JSON web token.</summary>
    Jwt,

    /// <summary>Secret without a more specific kind.</summary>
    GenericSecret,

    /// <summary>Text that could not be classified safely, for example after a regular expression timeout.</summary>
    NeedsReview,
}
