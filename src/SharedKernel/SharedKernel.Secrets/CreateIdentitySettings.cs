namespace SharedKernel.Secrets;

/// <summary>What <see cref="SecretsRepository.CreateIdentity"/> creates.</summary>
public sealed record CreateIdentitySettings
{
    /// <summary>Gets the absolute path of the identity file to create. It must not exist.</summary>
    public required string IdentityPath { get; init; }

    /// <summary>Gets a value indicating whether to create the file; without it the operation only previews.</summary>
    public bool Force { get; init; }
}
