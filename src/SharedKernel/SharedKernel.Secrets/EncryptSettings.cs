namespace PandaBear.Secrets;

/// <summary>What <see cref="SecretsRepository.Encrypt"/> encrypts.</summary>
public sealed record EncryptSettings
{
    /// <summary>Gets the new encrypted file, relative to the root, such as <c>secrets/app.sops.yaml</c>.</summary>
    public required string File { get; init; }

    /// <summary>Gets a value indicating whether to read the plaintext and create the file; without it the operation only previews and reads nothing.</summary>
    public bool Force { get; init; }

    /// <summary>Gets the function that opens the plaintext, such as standard input. It is called only with <see cref="Force"/>.</summary>
    public required Func<Stream> OpenPlaintext { get; init; }
}
