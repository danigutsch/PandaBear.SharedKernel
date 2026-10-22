namespace PandaBear.VaultManager.Tool.Secrets;

/// <summary>Meaning of the label in a <c>label: value</c> notes line.</summary>
internal enum KeyClass
{
    /// <summary>No secret-bearing label.</summary>
    None = 0,

    /// <summary>Password.</summary>
    Password,

    /// <summary>PIN.</summary>
    Pin,

    /// <summary>User name or email.</summary>
    Username,

    /// <summary>Token, API key, or client secret.</summary>
    Token,

    /// <summary>Recovery or backup codes.</summary>
    Recovery,

    /// <summary>Seed phrase.</summary>
    Seed,

    /// <summary>TOTP secret.</summary>
    Totp,

    /// <summary>Security question.</summary>
    Question,

    /// <summary>Security answer.</summary>
    Answer,

    /// <summary>IBAN.</summary>
    Iban,

    /// <summary>BIC or SWIFT code.</summary>
    Bic,
}
