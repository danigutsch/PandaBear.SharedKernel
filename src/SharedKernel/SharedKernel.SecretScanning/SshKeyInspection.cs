namespace PandaBear.SecretScanning;

/// <summary>What <see cref="SshKeyConverter.Inspect"/> learned about a private key without converting it.</summary>
/// <param name="Format">Container format, for example <c>OpenSSH</c>, <c>PKCS#1</c>, or <c>PKCS#8</c>.</param>
/// <param name="Algorithm">Key algorithm, when known.</param>
/// <param name="IsEncrypted">Whether the key is protected by a passphrase.</param>
/// <param name="CanConvert">Whether <see cref="SshKeyConverter.Convert"/> can convert the key.</param>
/// <param name="Problem">Why the key cannot be converted, or <see langword="null"/>. It never repeats key material.</param>
public sealed record SshKeyInspection(string Format, string? Algorithm, bool IsEncrypted, bool CanConvert, string? Problem);
