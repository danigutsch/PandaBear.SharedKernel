namespace SharedKernel.Vault.Bitwarden.Crypto;

/// <summary>Password key derivation functions used by Bitwarden password-protected exports.</summary>
internal enum KdfType
{
    /// <summary>PBKDF2 with HMAC-SHA256.</summary>
    Pbkdf2Sha256 = 0,

    /// <summary>Argon2id version 1.3.</summary>
    Argon2id = 1,
}
