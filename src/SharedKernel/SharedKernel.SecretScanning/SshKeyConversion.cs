namespace SharedKernel.SecretScanning;

/// <summary>The result of <see cref="SshKeyConverter.Convert"/>: the key pair, or why the key was not converted.</summary>
/// <param name="Key">The converted key pair, or <see langword="null"/> when the key was not converted.</param>
/// <param name="Problem">Why the key was not converted, or <see langword="null"/>. It never repeats key material.</param>
public sealed record SshKeyConversion(SshKeyPair? Key, string? Problem);
