using PandaBear.VaultManager.Tool.Model;

namespace PandaBear.VaultManager.Tool.Secrets;

/// <summary>Result of converting a private key for a Bitwarden SSH key item.</summary>
/// <param name="Key">Converted key data, when conversion succeeded.</param>
/// <param name="Problem">Why the key was not converted, without any value.</param>
internal sealed record SshKeyConversion(SshKeyData? Key, string? Problem);
