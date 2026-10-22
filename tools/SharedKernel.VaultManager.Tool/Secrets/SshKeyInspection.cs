namespace PandaBear.VaultManager.Tool.Secrets;

/// <summary>What a private key is, determined without converting it.</summary>
/// <param name="Format">Container format, for example <c>OpenSSH</c>, <c>PKCS#1</c>, or <c>PKCS#8</c>.</param>
/// <param name="Algorithm">Key algorithm, when known.</param>
/// <param name="IsEncrypted">Whether the key is protected by a passphrase.</param>
/// <param name="CanConvert">Whether the key can become a Bitwarden SSH key item.</param>
/// <param name="Problem">Why the key cannot be converted, without any value.</param>
internal sealed record SshKeyInspection(string Format, string? Algorithm, bool IsEncrypted, bool CanConvert, string? Problem);
