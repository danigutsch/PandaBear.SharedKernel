namespace PandaBear.Secrets;

/// <summary>What <see cref="SecretsRepository.CheckRecipients"/> checks.</summary>
public sealed record CheckRecipientsSettings
{
    /// <summary>Gets the encrypted YAML or JSON file, relative to the root.</summary>
    public required string File { get; init; }

    /// <summary>Gets the absolute paths of identity files that must be able to decrypt the file.</summary>
    public IReadOnlyList<string> ProveWithIdentities { get; init; } = [];

    /// <summary>Gets the absolute paths of identity files that must not be able to decrypt the file, such as revoked keys.</summary>
    public IReadOnlyList<string> ProveAbsentIdentities { get; init; } = [];
}
