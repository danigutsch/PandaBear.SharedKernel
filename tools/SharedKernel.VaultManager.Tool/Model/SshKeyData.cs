using System.Text;

namespace PandaBear.VaultManager.Tool.Model;

/// <summary>SSH key pair data.</summary>
internal sealed record SshKeyData
{
    /// <summary>Private key in OpenSSH or PKCS#8 PEM form.</summary>
    public string? PrivateKey
    {
        get; init;
    }

    /// <summary>Public key in OpenSSH authorized_keys form.</summary>
    public string? PublicKey
    {
        get; init;
    }

    /// <summary>Public key fingerprint.</summary>
    public string? KeyFingerprint
    {
        get; init;
    }

    /// <summary>Prints only non-secret members, so values never reach logs or messages through <c>ToString</c>.</summary>
    private bool PrintMembers(StringBuilder builder)
    {
        builder.Append("KeyFingerprint = ").Append(KeyFingerprint);
        return true;
    }
}
