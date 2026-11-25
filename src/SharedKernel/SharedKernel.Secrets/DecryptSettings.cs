namespace SharedKernel.Secrets;

/// <summary>What <see cref="SecretsRepository.Decrypt"/> decrypts and where the plaintext goes.</summary>
public sealed record DecryptSettings
{
    /// <summary>Gets the encrypted file, or <c>file#key.path</c> for one value, relative to the root.</summary>
    public required string Target { get; init; }

    /// <summary>Gets the absolute path of the age identity file. Never the key itself.</summary>
    public required string IdentityPath { get; init; }

    /// <summary>Gets a new file relative to the root to write the plaintext to, or <see langword="null"/> to write it to <see cref="OpenPlaintextOutput"/>.</summary>
    public string? OutputPath { get; init; }

    /// <summary>Gets a value indicating whether writing to <see cref="OutputPath"/> is allowed; it is required with it.</summary>
    public bool Force { get; init; }

    /// <summary>Gets the function that opens the destination of the plaintext when <see cref="OutputPath"/> is <see langword="null"/>, such as standard output.</summary>
    public required Func<Stream> OpenPlaintextOutput { get; init; }
}
