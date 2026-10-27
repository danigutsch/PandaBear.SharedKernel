using System.Text.Json.Serialization;

namespace PandaBear.VaultManager.Tool.Formats.Bitwarden;

/// <summary>Bitwarden password-protected export wrapper.</summary>
internal sealed record PasswordProtectedEnvelopeDto
{
    /// <summary>Whether the data is encrypted; true for this envelope.</summary>
    public bool Encrypted
    {
        get; init;
    }

    /// <summary>Whether the export is protected by a password; true for this envelope.</summary>
    public bool PasswordProtected
    {
        get; init;
    }

    /// <summary>Base64 salt used to derive the key from the password.</summary>
    public string? Salt
    {
        get; init;
    }

    /// <summary>Key derivation function: 0 PBKDF2-SHA256, 1 Argon2id.</summary>
    public int KdfType
    {
        get; init;
    }

    /// <summary>Key derivation iteration count.</summary>
    public int KdfIterations
    {
        get; init;
    }

    /// <summary>Argon2id memory in MiB, written only for Argon2id.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? KdfMemory
    {
        get; init;
    }

    /// <summary>Argon2id parallelism, written only for Argon2id.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? KdfParallelism
    {
        get; init;
    }

    /// <summary>Encrypted validation string that lets the importer check the password before decrypting the data.</summary>
    [JsonPropertyName("encKeyValidation_DO_NOT_EDIT")]
    public string? EncKeyValidation
    {
        get; init;
    }

    /// <summary>Encrypted export content as a type 2 encrypted string.</summary>
    public string? Data
    {
        get; init;
    }
}
