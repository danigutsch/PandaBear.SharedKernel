namespace PandaBear.SecretScanning;

/// <summary>An SSH key converted by <see cref="SshKeyConverter"/>.</summary>
/// <param name="PrivateKey">The private key, in OpenSSH or PKCS#8 PEM form.</param>
/// <param name="PublicKey">The public key line, such as <c>ssh-ed25519 AAAA...</c>, without a comment.</param>
/// <param name="Fingerprint">The <c>SHA256:</c> fingerprint as <c>ssh-keygen -lf</c> prints it.</param>
public sealed record SshKeyPair(string PrivateKey, string PublicKey, string Fingerprint)
{
    /// <summary>Describes the key pair without its private key.</summary>
    /// <returns>The public key and the fingerprint.</returns>
    public override string ToString() => $"{PublicKey} {Fingerprint}";
}
