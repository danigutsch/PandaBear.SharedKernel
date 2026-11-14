using System.Text;

namespace PandaBear.VaultManager.Tool.Model;

/// <summary>Passkey stored on a login, in Bitwarden export shape.</summary>
internal sealed record Fido2Credential
{
    /// <summary>Credential identifier: a GUID or <c>b64.</c> followed by base64url.</summary>
    public required string CredentialId { get; init; }

    /// <summary>Key type, normally <c>public-key</c>.</summary>
    public string KeyType { get; init; } = "public-key";

    /// <summary>Key algorithm, normally <c>ECDSA</c>.</summary>
    public string KeyAlgorithm { get; init; } = "ECDSA";

    /// <summary>Key curve, normally <c>P-256</c>.</summary>
    public string KeyCurve { get; init; } = "P-256";

    /// <summary>Base64url PKCS#8 private key without padding.</summary>
    public required string KeyValue { get; init; }

    /// <summary>Relying party identifier.</summary>
    public required string RpId { get; init; }

    /// <summary>Base64url user handle.</summary>
    public string? UserHandle { get; init; }

    /// <summary>User name shown by the relying party.</summary>
    public string? UserName { get; init; }

    /// <summary>Signature counter as a decimal string.</summary>
    public string Counter { get; init; } = "0";

    /// <summary>Relying party display name.</summary>
    public string? RpName { get; init; }

    /// <summary>User display name.</summary>
    public string? UserDisplayName { get; init; }

    /// <summary>Whether the credential is discoverable, as <c>true</c> or <c>false</c>.</summary>
    public string Discoverable { get; init; } = "true";

    /// <summary>When the passkey was created.</summary>
    public DateTimeOffset CreationDate { get; init; }

    /// <summary>Prints only non-secret members, so values never reach logs or messages through <c>ToString</c>.</summary>
    private bool PrintMembers(StringBuilder builder)
    {
        builder.Append("RpId = ").Append(RpId);
        return true;
    }
}
