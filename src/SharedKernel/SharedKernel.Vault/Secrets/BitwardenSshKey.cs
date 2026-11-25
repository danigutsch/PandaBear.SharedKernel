using SharedKernel.Vault.Model;

namespace SharedKernel.Vault.Secrets;

/// <summary>A private key converted into Bitwarden SSH key data, or why it was not converted.</summary>
/// <param name="Key">The SSH key data, or <see langword="null"/>.</param>
/// <param name="Problem">Why the key was not converted, or <see langword="null"/>.</param>
internal sealed record BitwardenSshKey(SshKeyData? Key, string? Problem);
