namespace PandaBear.VaultManager.Tool.Crypto;

/// <summary>Key derivation settings of a password-protected export.</summary>
/// <param name="Type">Derivation function.</param>
/// <param name="Iterations">Iteration count.</param>
/// <param name="MemoryMiB">Argon2id memory in MiB; unused for PBKDF2.</param>
/// <param name="Parallelism">Argon2id lanes; unused for PBKDF2.</param>
internal sealed record KdfConfig(KdfType Type, int Iterations, int? MemoryMiB = null, int? Parallelism = null)
{
    /// <summary>Bitwarden's current PBKDF2 default: 600,000 iterations.</summary>
    public static KdfConfig DefaultPbkdf2 { get; } = new(KdfType.Pbkdf2Sha256, 600_000);

    /// <summary>
    /// Rejects settings outside the ranges Bitwarden accepts. Runs before any derivation, because Argon2id allocates
    /// the requested memory up front and an unchecked file could exhaust it.
    /// </summary>
    /// <exception cref="InvalidDataException">The settings are outside the accepted ranges.</exception>
    public void Validate()
    {
        switch (Type)
        {
            case KdfType.Pbkdf2Sha256:
                Require(Iterations is >= 5_000 and <= 2_000_000, "PBKDF2 iterations must be between 5,000 and 2,000,000.");
                break;
            case KdfType.Argon2id:
                Require(Iterations is >= 2 and <= 10, "Argon2id iterations must be between 2 and 10.");
                Require(MemoryMiB is >= 16 and <= 1024, "Argon2id memory must be between 16 and 1024 MiB.");
                Require(Parallelism is >= 1 and <= 16, "Argon2id parallelism must be between 1 and 16.");
                break;
            default:
                throw new InvalidDataException("Unsupported key derivation type.");
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidDataException(message);
        }
    }
}
