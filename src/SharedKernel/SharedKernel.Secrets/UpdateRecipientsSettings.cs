namespace SharedKernel.Secrets;

/// <summary>What <see cref="SecretsRepository.UpdateRecipients"/> updates.</summary>
public sealed record UpdateRecipientsSettings
{
    /// <summary>Gets the encrypted YAML or JSON file, relative to the root.</summary>
    public required string File { get; init; }

    /// <summary>Gets the absolute path of an identity file that can decrypt the data key; required with <see cref="Force"/>.</summary>
    public string? IdentityPath { get; init; }

    /// <summary>Gets a value indicating whether to write the file; without it the operation only previews.</summary>
    public bool Force { get; init; }
}
